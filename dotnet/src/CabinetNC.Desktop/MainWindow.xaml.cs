using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CabinetNC.Application.Projects;
using CabinetNC.Compute.Contracts;
using CabinetNC.Desktop.Worker;
using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Machines;
using CabinetNC.Domain.Manufacturing;
using CabinetNC.Domain.Nesting;
using CabinetNC.Domain.Parts;
using CabinetNC.FusionPackage;
using CabinetNC.Infrastructure.Library;
using CabinetNC.Infrastructure.Projects;
using Microsoft.Win32;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using PanelPart = CabinetNC.Domain.Parts.Panel;

namespace CabinetNC.Desktop;

public partial class MainWindow : Window
{
    readonly ProjectSession _session = new();
    readonly WorkerProcessHost _worker = new();
    readonly SqliteProjectStore _store = new();
    WorkshopLibrary _library = WorkshopLibraryStore.Load();
    readonly HashSet<string> _locked = new(StringComparer.Ordinal);
    PanelPart? _selected;
    PanelPart? _clipboardPanel;
    StartNestingReply? _nest;
    bool _showNest;
    string _stage = "load";
    string _module = "production";
    bool _nestBusy;
    bool _stageChanging;
    bool _enableContour = true;
    bool _enableDrill = true;
    bool _enableGroove = true;
    string? _activeToolId;
    IReadOnlyList<CamFrame> _camFrames = [];
    int _camFrameIndex;
    readonly DispatcherTimer _camTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };

    // drag state
    string? _dragMode; // geom | nest
    GeomInteraction.Hit? _geomHit;
    PanelPart? _geomStart;
    string? _nestDragPanelId;
    double _nestStartMx, _nestStartMy, _nestOrigOx, _nestOrigOy;
    GeomInteraction.View? _geomView;
    float _nestPad, _nestScale, _nestSheetH, _nestSheetW;
    int _surfaceW, _surfaceH;
    double _dpiX = 1, _dpiY = 1;
    string? _hoverHint;
    IReadOnlyList<CutOp> _opsOverlay = [];

    public MainWindow()
    {
        InitializeComponent();
        foreach (var m in MachineCatalog.All)
        {
            MachineCombo.Items.Add(m);
            MachineComboModule.Items.Add(m);
        }
        MachineCombo.SelectedValue = "nesting_router_6";
        MachineComboModule.SelectedValue = "nesting_router_6";
        ApplyLibraryToSettingsUi();
        ApplyLibraryToNestBoxes();
        StageTabs.SelectedIndex = 0;
        HighlightModule();
        ApplyModuleVisibility();
        ApplyStageVisibility();
        UpdateCanvasHint();
        UpdateStageChrome();
        RefreshWorkflowDots();
        RefreshEmptyState();
        _camTimer.Tick += (_, _) => StepCam(1);
        PreviewKeyDown += OnPreviewKeyDown;

        Loaded += async (_, _) =>
        {
            await RefreshWorkerAsync();
            UpdateStageChrome();
            RefreshWorkflowDots();
            RefreshEmptyState();
            SetStatus("生产加工 · 先载入方案");
        };
        Closed += async (_, _) =>
        {
            _camTimer.Stop();
            await _worker.DisposeAsync();
        };
    }

    string SelectedMachineId() =>
        MachineCombo.SelectedValue as string
        ?? (MachineCombo.SelectedItem as MachineProfile)?.Id
        ?? "nesting_router_6";

    void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Key == Key.Z)
            {
                if (_session.TryUndo())
                {
                    AfterHistoryRestore();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Y)
            {
                if (_session.TryRedo())
                {
                    AfterHistoryRestore();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.C)
            {
                CopySelectedToClipboard();
                e.Handled = true;
            }
            else if (e.Key == Key.X)
            {
                CutSelectedPanel();
                e.Handled = true;
            }
            else if (e.Key == Key.V)
            {
                PasteClipboardPanel();
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.Delete)
        {
            if (SelectedFeature() is not null)
                OnGeomDeleteFeatureClick(sender, e);
            else
                OnDeletePanelClick(sender, e);
            e.Handled = true;
        }
    }

    void AfterHistoryRestore()
    {
        InvalidateManufacturingOutputs("undo/redo");
        PartList.Items.Clear();
        if (_session.Package is not null)
            foreach (var p in _session.Package.Panels)
                PartList.Items.Add(p);
        _selected = PartList.Items.OfType<PanelPart>().FirstOrDefault(p => p.PanelId == _selected?.PanelId)
            ?? PartList.Items.OfType<PanelPart>().FirstOrDefault();
        if (_selected is not null)
            PartList.SelectedItem = _selected;
        RefreshGeomRail();
        RefreshNestReport();
        UpdateCanvasHint();
        CanvasHost.InvalidateVisual();
    }

    void OnStageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, StageTabs) || _stageChanging) return;
        if (StageTabs.SelectedItem is not TabItem tab) return;
        var next = tab.Tag as string ?? "load";

        // Gate: no package → only 载入方案
        if (_session.Package is null && next != "load")
        {
            _stageChanging = true;
            StageTabs.SelectedIndex = 0;
            _stageChanging = false;
            SetStatus("请先载入方案");
            RefreshEmptyState();
            return;
        }

        _stage = next;
        _showNest = _stage is "nest" or "ops";
        ApplyStageVisibility();
        UpdateCanvasHint();
        UpdateStageChrome();
        RefreshWorkflowDots();
        RefreshEmptyState();
        CanvasHost.InvalidateVisual();

        if (_stage == "nest")
        {
            SyncNestSettingsFromPackage();
            RefreshNestReport();
            SetStatus(_nest is { Ok: true }
                ? $"密排 · placed={_nest.Placements.Count} sheets={_nest.SheetCount}"
                : "密排 · 打包中…");
            if (_nest is not { Ok: true } && _session.Package is not null && !_nestBusy)
                _ = RunNestAsync(withNc: false);
        }
        else if (_stage == "load")
        {
            SetStatus(_session.Package is null ? "载入方案 · woodjob / cut-package" : "载入方案 · 检视/编辑板件");
            RefreshGeomRail();
        }
        else if (_stage == "stock")
        {
            SyncNestSettingsFromPackage();
            SetStatus("板材与设备 · 设置板材尺寸 / 机型");
        }
        else if (_stage == "ops")
        {
            RebuildOpsOverlay();
            SetStatus(_nest is { Ok: true } ? "刀路 · 绿轮廓 · 蓝孔" : "刀路 · 先完成密排");
        }
        else if (_stage == "out")
            SetStatus(string.IsNullOrWhiteSpace(NcPreview.Text) ? "导出 · 尚无 NC" : "导出 · 可保存 NC");
    }

    void ApplyStageVisibility()
    {
        var hasPkg = _session.Package is not null;
        TabStock.IsEnabled = hasPkg;
        TabNest.IsEnabled = hasPkg;
        TabOps.IsEnabled = hasPkg;
        TabOut.IsEnabled = hasPkg;

        var showGeomRail = _stage == "load" && hasPkg;
        var showNestRail = _stage is "stock" or "nest";
        var showNc = _stage is "ops" or "out";
        var showCanvas = _stage is not "out";
        GeomPane.Visibility = showGeomRail ? Visibility.Visible : Visibility.Collapsed;
        NestPane.Visibility = showNestRail ? Visibility.Visible : Visibility.Collapsed;
        NcPane.Visibility = showNc ? Visibility.Visible : Visibility.Collapsed;
        CanvasPane.Visibility = showCanvas ? Visibility.Visible : Visibility.Collapsed;

        NestPaneTitle.Text = _stage == "stock" ? "板材与设备" : "密排参数";
        NestApplyBtn.Visibility = _stage == "nest" ? Visibility.Visible : Visibility.Collapsed;
        NestNcBtn.Visibility = _stage == "nest" ? Visibility.Visible : Visibility.Collapsed;

        if (_stage == "out")
        {
            Grid.SetColumn(NcPane, 1);
            Grid.SetColumnSpan(NcPane, 2);
            NcCol.Width = new GridLength(0);
            NcPaneTitle.Text = "导出 · NC / DXF / 工单";
            OutSaveNcBtn.Visibility = Visibility.Visible;
            OutExportPanel.Visibility = Visibility.Visible;
            OpsListBox.Visibility = Visibility.Collapsed;
            CamSimPanel.Visibility = Visibility.Collapsed;
            OpsMeta.Text = string.IsNullOrWhiteSpace(NcPreview.Text) ? "无 NC — 先密排并生成加工档" : "可导出 NC / DXF / 工单 / JSON";
            RefreshPreflightMeta();
        }
        else
        {
            Grid.SetColumn(GeomPane, 2);
            Grid.SetColumn(NestPane, 2);
            Grid.SetColumn(NcPane, 2);
            Grid.SetColumnSpan(GeomPane, 1);
            Grid.SetColumnSpan(NestPane, 1);
            Grid.SetColumnSpan(NcPane, 1);
            NcCol.Width = new GridLength(300);
            NcPaneTitle.Text = _stage == "ops" ? "刀路 / 加工档" : "NC";
            OutSaveNcBtn.Visibility = Visibility.Collapsed;
            OutExportPanel.Visibility = Visibility.Collapsed;
            OpsListBox.Visibility = _stage == "ops" ? Visibility.Visible : Visibility.Collapsed;
            CamSimPanel.Visibility = _stage == "ops" ? Visibility.Visible : Visibility.Collapsed;
            if (_stage == "ops")
            {
                OpsMeta.Text = _opsOverlay.Count > 0
                    ? $"ops {_opsOverlay.Count} · canvas overlay"
                    : "先密排 + 生成加工档";
                RefreshOpsListBox();
            }
        }

        RefreshOneClickExport();
    }

    void UpdateStageChrome()
    {
        StageHint.Text = _stage switch
        {
            "load" => _session.Package is null
                ? "载入方案: 打开 woodjob.zip / cut-package，或点「打开示例」"
                : "载入方案: 检视板件 · 拖孔/槽/边 · 右侧可加特征",
            "stock" => "板材与设备: 板宽/板长/边距/间距 · 选择机型",
            "nest" => "密排: 拖摆位 · 锁定后重排保留 · 放下校验重叠",
            "ops" => "刀路与加工档: 绿虚线=轮廓 · 蓝十字=孔（只读）",
            "out" => "导出: 预览 NC · 导出 NC 或一键导出",
            _ => "",
        };
        AllowOverlapChk.Visibility = _stage == "nest" ? Visibility.Visible : Visibility.Collapsed;
        LockPlaceBtn.Visibility = _stage == "nest" ? Visibility.Visible : Visibility.Collapsed;
    }

    void RefreshWorkflowDots()
    {
        WfDots.Children.Clear();
        var pkg = _session.Package;
        var hasPkg = pkg?.Panels.Count > 0;
        var hasNest = _nest is { Ok: true, Placements.Count: > 0 };
        var hasOps = _opsOverlay.Count > 0 || HasNcText();
        var hasNc = HasNcText();
        var stages = new (string Id, bool Done)[]
        {
            ("load", hasPkg),
            ("stock", hasPkg),
            ("nest", hasNest),
            ("ops", hasOps),
            ("out", hasNc),
        };
        foreach (var (id, done) in stages)
        {
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Margin = new Thickness(0, 0, 4, 0),
                Fill = done ? new SolidColorBrush(Color.FromRgb(0x22, 0x77, 0xCC)) : new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xBB)),
                Stroke = id == _stage
                    ? new SolidColorBrush(Color.FromRgb(0x22, 0x77, 0xCC))
                    : new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
                StrokeThickness = id == _stage ? 2 : 1,
            };
            WfDots.Children.Add(dot);
        }
        RefreshOneClickExport();
    }

    bool HasNcText() =>
        !string.IsNullOrWhiteSpace(NcPreview.Text) && !NcPreview.Text.StartsWith("//");

    void RefreshOneClickExport() =>
        OneClickExportBtn.IsEnabled = _nest is { Ok: true, Placements.Count: > 0 } && HasNcText();

    void RefreshEmptyState()
    {
        var empty = _session.Package is null && _stage == "load" && _module == "production";
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
    }

    void UpdateCanvasHint()
    {
        var needHint = _stage == "nest" && _nest is not { Ok: true } && _session.Package is not null;
        CanvasHint.Visibility = needHint ? Visibility.Visible : Visibility.Collapsed;
        CanvasHint.Text = "密排中… 或点「应用并重排」";
    }

    void OnModuleClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag) return;
        _module = tag;
        HighlightModule();
        ApplyModuleVisibility();
        RefreshActiveModule();
        RefreshEmptyState();
    }

    void RefreshActiveModule()
    {
        switch (_module)
        {
            case "remnants": RefreshRemnantsModule(); break;
            case "equipment": RefreshEquipmentModule(); break;
            case "routes": RefreshRoutesModule(); break;
            case "materials": RefreshMaterialsModule(); break;
            case "process": RefreshProcessModule(); break;
            case "settings": RefreshSettingsModule(); break;
        }
    }

    void HighlightModule()
    {
        void Style(Button b, bool on)
        {
            b.Background = on ? new SolidColorBrush(Color.FromRgb(0x2E, 0x4A, 0x6E)) : Brushes.Transparent;
            b.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
        }
        Style(ModProductionBtn, _module == "production");
        Style(ModRemnantsBtn, _module == "remnants");
        Style(ModEquipmentBtn, _module == "equipment");
        Style(ModRoutesBtn, _module == "routes");
        Style(ModMaterialsBtn, _module == "materials");
        Style(ModProcessBtn, _module == "process");
        Style(ModSettingsBtn, _module == "settings");
    }

    void ApplyModuleVisibility()
    {
        ProductionHost.Visibility = _module == "production" ? Visibility.Visible : Visibility.Collapsed;
        RemnantsHost.Visibility = _module == "remnants" ? Visibility.Visible : Visibility.Collapsed;
        EquipmentHost.Visibility = _module == "equipment" ? Visibility.Visible : Visibility.Collapsed;
        RoutesHost.Visibility = _module == "routes" ? Visibility.Visible : Visibility.Collapsed;
        MaterialsHost.Visibility = _module == "materials" ? Visibility.Visible : Visibility.Collapsed;
        ProcessHost.Visibility = _module == "process" ? Visibility.Visible : Visibility.Collapsed;
        SettingsHost.Visibility = _module == "settings" ? Visibility.Visible : Visibility.Collapsed;
    }

    void PersistLibrary()
    {
        WorkshopLibraryStore.Save(_library);
        SetStatus($"库已保存 · {WorkshopLibraryStore.DefaultPath()}");
    }

    void OnLibrarySaveClick(object sender, RoutedEventArgs e) => PersistLibrary();

    void OnGotoProductionClick(object sender, RoutedEventArgs e)
    {
        _module = "production";
        HighlightModule();
        ApplyModuleVisibility();
        RefreshEmptyState();
    }

    // ----- 补板库 -----
    void RefreshRemnantsModule()
    {
        RemnantsList.Items.Clear();
        foreach (var r in _library.Remnants)
            RemnantsList.Items.Add(
                $"{(r.UseInNest ? "[Nest]" : "[—]")} {r.Id} · {r.WidthMm:0.#}x{r.LengthMm:0.#}x{r.ThicknessMm:0.#} · {r.Material ?? "—"} · {r.Note ?? ""}");
        RemnantsMeta.Text =
            $"补板 {_library.Remnants.Count} · 参与密排 {_library.Remnants.Count(x => x.UseInNest)} · 库 {WorkshopLibraryStore.DefaultPath()}";
    }

    void OnRemnantToggleNestClick(object sender, RoutedEventArgs e)
    {
        var i = RemnantsList.SelectedIndex;
        if (i < 0 || i >= _library.Remnants.Count) return;
        _library.Remnants[i].UseInNest = !_library.Remnants[i].UseInNest;
        PersistLibrary();
        RefreshRemnantsModule();
    }

    void OnRemnantAddClick(object sender, RoutedEventArgs e)
    {
        var w = ParseMm(RemWBox.Text, 0);
        var l = ParseMm(RemLBox.Text, 0);
        var t = ParseMm(RemTBox.Text, 18);
        if (w <= 0 || l <= 0)
        {
            SetStatus("补板宽/长须 > 0");
            return;
        }
        _library.Remnants.Add(new LibRemnant
        {
            Id = "REM-" + DateTime.Now.ToString("HHmmss"),
            WidthMm = w,
            LengthMm = l,
            ThicknessMm = t,
            Material = string.IsNullOrWhiteSpace(RemMatBox.Text) ? null : RemMatBox.Text.Trim(),
        });
        PersistLibrary();
        RefreshRemnantsModule();
    }

    void OnRemnantFromSheetClick(object sender, RoutedEventArgs e)
    {
        var sheet = _session.Package?.Sheets.FirstOrDefault();
        RemWBox.Text = (sheet?.WidthMm > 0 ? sheet.WidthMm : _library.Nest.DefaultSheetWidthMm).ToString("0.###");
        RemLBox.Text = (sheet?.LengthMm > 0 ? sheet.LengthMm / 2 : _library.Nest.DefaultSheetLengthMm / 2).ToString("0.###");
        RemTBox.Text = (sheet?.ThicknessMm > 0 ? sheet.ThicknessMm : 18).ToString("0.###");
        RemMatBox.Text = sheet?.Material ?? "";
        SetStatus("已填入当前板材半长作为补板草稿 — 点「添加补板」确认");
    }

    void OnRemnantDeleteClick(object sender, RoutedEventArgs e)
    {
        var i = RemnantsList.SelectedIndex;
        if (i < 0 || i >= _library.Remnants.Count) return;
        _library.Remnants.RemoveAt(i);
        PersistLibrary();
        RefreshRemnantsModule();
    }

    // ----- 设备管理 -----
    void RefreshEquipmentModule()
    {
        EquipmentList.Items.Clear();
        foreach (var m in MachineCatalog.All)
            EquipmentList.Items.Add(m);
        EquipmentList.DisplayMemberPath = "Name";
        MachineComboModule.SelectedValue = SelectedMachineId();
        var cur = MachineCatalog.Get(SelectedMachineId());
        EquipmentDetail.Text = FormatMachine(cur);
        var idx = MachineCatalog.All.ToList().FindIndex(m => m.Id == cur.Id);
        if (idx >= 0) EquipmentList.SelectedIndex = idx;
    }

    static string FormatMachine(MachineProfile m) =>
        $"id: {m.Id}\nname: {m.Name}\ndialect: {m.Dialect}\nprogramEnd: {m.ProgramEnd}\n" +
        $"toolØ: {m.ToolDiameterMm} mm\nfeedXY: {m.FeedXyMmMin}\nfeedZ: {m.FeedZMmMin}\nrpm: {m.SpindleRpm}\n" +
        $"safeZ: {m.SafeZMm}\ncontour: {m.EnableContour}  drill: {m.EnableDrill}  groove: {m.EnableGroove}\n" +
        $"origin: {m.OriginNote ?? "—"}";

    void OnEquipmentListChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EquipmentList.SelectedItem is not MachineProfile m) return;
        EquipmentDetail.Text = FormatMachine(m);
        MachineComboModule.SelectedValue = m.Id;
    }

    void OnEquipmentApplyClick(object sender, RoutedEventArgs e)
    {
        if (MachineComboModule.SelectedValue is string id)
        {
            MachineCombo.SelectedValue = id;
            SetStatus($"已应用机型 · {id}");
        }
    }

    void OnMachineModuleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MachineComboModule.SelectedValue is string id)
            MachineCombo.SelectedValue = id;
    }

    // ----- 路线管理 -----
    void RefreshRoutesModule()
    {
        var hasPkg = _session.Package is not null;
        var hasNest = _nest is { Ok: true, Placements.Count: > 0 };
        var hasNc = HasNcText();
        RoutesMeta.Text =
            $"作业: {(hasPkg ? "已载入" : "未载入")}\n" +
            $"1 载入方案 …… {(hasPkg ? "✓" : "○")}\n" +
            $"2 板材与设备 … {(hasPkg ? "✓" : "○")}\n" +
            $"3 密排 ………… {(hasNest ? "✓" : "○")}\n" +
            $"4 刀路与加工档 {(hasNc || _opsOverlay.Count > 0 ? "✓" : "○")}\n" +
            $"5 导出 ………… {(hasNc ? "✓" : "○")}\n" +
            $"机型: {SelectedMachineId()}";
        RouteContourChk.IsChecked = _enableContour;
        RouteDrillChk.IsChecked = _enableDrill;
        RouteGrooveChk.IsChecked = _enableGroove;
        RebuildOpsOverlay();
        RoutesOpsList.Items.Clear();
        if (_opsOverlay.Count == 0)
            RoutesOpsList.Items.Add("无工序 — 先在生产加工中密排并生成加工档");
        else
        {
            foreach (var g in _opsOverlay.GroupBy(o => o.Op))
                RoutesOpsList.Items.Add($"{g.Key} × {g.Count()}");
        }
    }

    // ----- 原料管理 -----
    void RefreshMaterialsModule()
    {
        MaterialsList.Items.Clear();
        MaterialsList.Items.Add("— 车间材料库 —");
        foreach (var m in _library.Materials)
            MaterialsList.Items.Add($"[库] {m.Id} · {m.Name} · t={m.ThicknessMm:0.#} · {m.DensityHint ?? ""}");
        if (_session.Package is null)
        {
            MaterialsMeta.Text = $"库材料 {_library.Materials.Count} · 尚未载入方案";
            return;
        }
        MaterialsList.Items.Add("— 当前方案板材 —");
        foreach (var s in _session.Package.Sheets)
            MaterialsList.Items.Add($"[方案] {s.SheetId} · {s.Material ?? "—"} · {s.WidthMm:0.#}x{s.LengthMm:0.#} · t={s.ThicknessMm:0.#}");
        var mats = _session.Package.Panels.Select(p => p.Material).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x);
        MaterialsList.Items.Add("— 方案材料用量 —");
        foreach (var name in mats)
            MaterialsList.Items.Add($"[用] {name} · panels={_session.Package.Panels.Count(p => p.Material == name)}");
        MaterialsMeta.Text =
            $"库 {_library.Materials.Count} · 方案 sheets={_session.Package.Sheets.Count} panels={_session.Package.Panels.Count}";
    }

    void OnMaterialAddClick(object sender, RoutedEventArgs e)
    {
        var name = (MatNameBox.Text ?? "").Trim();
        if (string.IsNullOrEmpty(name)) return;
        var id = "mat_" + name.Replace(' ', '_').ToLowerInvariant();
        var existing = _library.Materials.FindIndex(m => m.Id == id || m.Name == name);
        var row = new LibMaterial
        {
            Id = id,
            Name = name,
            ThicknessMm = ParseMm(MatThickBox.Text, 18),
            DensityHint = string.IsNullOrWhiteSpace(MatHintBox.Text) ? null : MatHintBox.Text.Trim(),
        };
        if (existing >= 0) _library.Materials[existing] = row;
        else _library.Materials.Add(row);
        PersistLibrary();
        RefreshMaterialsModule();
    }

    void OnMaterialDeleteClick(object sender, RoutedEventArgs e)
    {
        // only delete library rows: map selected text back
        if (MaterialsList.SelectedItem is not string s || !s.StartsWith("[库] ")) return;
        var id = s["[库] ".Length..].Split('·')[0].Trim();
        _library.Materials.RemoveAll(m => m.Id == id);
        PersistLibrary();
        RefreshMaterialsModule();
    }

    void OnMaterialSyncPackageClick(object sender, RoutedEventArgs e)
    {
        if (_session.Package is null)
        {
            SetStatus("无方案可同步");
            return;
        }
        foreach (var name in _session.Package.Panels.Select(p => p.Material).Where(m => !string.IsNullOrEmpty(m)).Distinct())
        {
            var id = "mat_" + name!.Replace(' ', '_').ToLowerInvariant();
            if (_library.Materials.Any(m => m.Id == id || m.Name == name)) continue;
            var t = _session.Package.Panels.First(p => p.Material == name).ThicknessMm;
            _library.Materials.Add(new LibMaterial { Id = id, Name = name, ThicknessMm = t > 0 ? t : 18 });
        }
        PersistLibrary();
        RefreshMaterialsModule();
        SetStatus("已从方案同步材料到车间库");
    }

    // ----- 工艺模版 -----
    void RefreshProcessModule()
    {
        ProcessToolsList.Items.Clear();
        foreach (var t in _library.Tools)
            ProcessToolsList.Items.Add($"{t.Id} · {t.Name} · Ø{t.DiameterMm:0.#} · F{t.FeedXyMmMin:0}/Z{t.FeedZMmMin:0} · {t.SpindleRpm:0}rpm");
        var activeIndex = _library.Tools.FindIndex(t => t.Id == _activeToolId);
        if (activeIndex >= 0) ProcessToolsList.SelectedIndex = activeIndex;
        RebuildOpsOverlay();
        ProcessOpsList.Items.Clear();
        if (_opsOverlay.Count == 0)
            ProcessOpsList.Items.Add("无作业工序");
        else
            foreach (var g in _opsOverlay.GroupBy(o => o.Op))
                ProcessOpsList.Items.Add($"{g.Key} × {g.Count()}");
        var activeTool = _library.Tools.FirstOrDefault(t => t.Id == _activeToolId);
        ProcessMeta.Text =
            $"刀具 {_library.Tools.Count} · 作业工序 {_opsOverlay.Count} · 机型 {SelectedMachineId()} · " +
            $"当前刀具 {(activeTool?.Name ?? "机型默认")}";
    }

    void OnToolAddClick(object sender, RoutedEventArgs e)
    {
        var name = (ToolNameBox.Text ?? "").Trim();
        if (string.IsNullOrEmpty(name)) return;
        var id = "tool_" + name.Replace(' ', '_').ToLowerInvariant();
        var row = new LibTool
        {
            Id = id,
            Name = name,
            MachineId = SelectedMachineId(),
            DiameterMm = ParseMm(ToolDiaBox.Text, 6),
            FeedXyMmMin = ParseMm(ToolFeedXyBox.Text, 3000),
            FeedZMmMin = 500,
            SpindleRpm = ParseMm(ToolRpmBox.Text, 18000),
        };
        var i = _library.Tools.FindIndex(t => t.Id == id);
        if (i >= 0) _library.Tools[i] = row;
        else _library.Tools.Add(row);
        PersistLibrary();
        RefreshProcessModule();
    }

    void OnProcessToolSelected(object sender, SelectionChangedEventArgs e)
    {
        var i = ProcessToolsList.SelectedIndex;
        if (i < 0 || i >= _library.Tools.Count) return;
        var t = _library.Tools[i];
        ToolNameBox.Text = t.Name;
        ToolDiaBox.Text = t.DiameterMm.ToString("0.###");
        ToolFeedXyBox.Text = t.FeedXyMmMin.ToString("0.###");
        ToolRpmBox.Text = t.SpindleRpm.ToString("0.###");
    }

    void OnToolApplyClick(object sender, RoutedEventArgs e)
    {
        var i = ProcessToolsList.SelectedIndex;
        if (i < 0 || i >= _library.Tools.Count)
        {
            SetStatus("请先选择刀具");
            return;
        }
        var tool = _library.Tools[i];
        _activeToolId = tool.Id;
        CamOffsetBox.Text = (tool.DiameterMm / 2).ToString("0.###");
        RebuildOpsOverlay();
        RegenerateNcFromCurrentOps();
        RefreshProcessModule();
        CanvasHost.InvalidateVisual();
        SetStatus($"已应用刀具 · {tool.Name} Ø{tool.DiameterMm:0.###} · F{tool.FeedXyMmMin:0}");
    }

    void OnToolDeleteClick(object sender, RoutedEventArgs e)
    {
        var i = ProcessToolsList.SelectedIndex;
        if (i < 0 || i >= _library.Tools.Count) return;
        if (_library.Tools[i].Id == _activeToolId) _activeToolId = null;
        _library.Tools.RemoveAt(i);
        PersistLibrary();
        RefreshProcessModule();
    }

    void OnToolResetFromMachinesClick(object sender, RoutedEventArgs e)
    {
        _library.Tools = WorkshopLibraryStore.CreateDefault().Tools;
        PersistLibrary();
        RefreshProcessModule();
    }

    // ----- 参数设置 -----
    void ApplyLibraryToSettingsUi()
    {
        SetSheetWBox.Text = _library.Nest.DefaultSheetWidthMm.ToString("0.###");
        SetSheetLBox.Text = _library.Nest.DefaultSheetLengthMm.ToString("0.###");
        SetSpacingBox.Text = _library.Nest.SpacingMm.ToString("0.###");
        SetBorderBox.Text = _library.Nest.BorderMm.ToString("0.###");
        SetAllowRotChk.IsChecked = _library.Nest.AllowRotation;
    }

    void ApplyLibraryToNestBoxes()
    {
        StockWidthBox.Text = _library.Nest.DefaultSheetWidthMm.ToString("0.###");
        StockLengthBox.Text = _library.Nest.DefaultSheetLengthMm.ToString("0.###");
        NestSpacingBox.Text = _library.Nest.SpacingMm.ToString("0.###");
        NestBorderBox.Text = _library.Nest.BorderMm.ToString("0.###");
        NestAllowRotChk.IsChecked = _library.Nest.AllowRotation;
    }

    void RefreshSettingsModule()
    {
        ApplyLibraryToSettingsUi();
        SettingsMeta.Text =
            $"库路径: {WorkshopLibraryStore.DefaultPath()}\n" +
            $"savedAt: {_library.SavedAt ?? "—"}\n" +
            $"materials={_library.Materials.Count} tools={_library.Tools.Count} remnants={_library.Remnants.Count}";
    }

    void ReadSettingsUiIntoLibrary()
    {
        _library.Nest.DefaultSheetWidthMm = ParseMm(SetSheetWBox.Text, 1220);
        _library.Nest.DefaultSheetLengthMm = ParseMm(SetSheetLBox.Text, 2440);
        _library.Nest.SpacingMm = ParseMm(SetSpacingBox.Text, 12);
        _library.Nest.BorderMm = ParseMm(SetBorderBox.Text, 15);
        _library.Nest.AllowRotation = SetAllowRotChk.IsChecked == true;
    }

    void OnSettingsSaveClick(object sender, RoutedEventArgs e)
    {
        ReadSettingsUiIntoLibrary();
        PersistLibrary();
        RefreshSettingsModule();
    }

    void OnSettingsApplyClick(object sender, RoutedEventArgs e)
    {
        ReadSettingsUiIntoLibrary();
        PersistLibrary();
        ApplyLibraryToNestBoxes();
        SetStatus("参数已应用到生产加工排版框");
    }

    void OnSettingsResetClick(object sender, RoutedEventArgs e)
    {
        _library.Nest = new NestDefaults();
        PersistLibrary();
        RefreshSettingsModule();
    }


    void RebuildOpsOverlay()
    {
        _opsOverlay = [];
        if (_session.Package is null || _nest is not { Ok: true }) return;
        var places = _nest.Placements.Select(p => new NestPlacement
        {
            PanelId = p.PanelId,
            SheetIndex = p.SheetIndex,
            OffsetX = p.OffsetX,
            OffsetY = p.OffsetY,
            RotationDeg = p.RotationDeg,
        }).ToList();
        var raw = OpsPlanner.FeaturesToOps(
            _session.Package.Panels,
            _enableContour,
            _enableDrill,
            _enableGroove);
        _opsOverlay = OpsPlanner.AttachToNest(raw, places);
        if (CamOffsetChk.IsChecked == true)
            _opsOverlay = ContourToolOffset.Apply(
                _opsOverlay,
                ParseMm(CamOffsetBox.Text, ActiveProfileForCam().ToolDiameterMm / 2));
        OpsMeta.Text =
            $"ops {_opsOverlay.Count} · contour={_opsOverlay.Count(o => o.Op == "contour")} " +
            $"drill={_opsOverlay.Count(o => o.Op == "drill")} groove={_opsOverlay.Count(o => o.Op == "groove")}";
        RefreshOpsListBox();
        RefreshCamFrames();
        RefreshPreflightMeta();
    }

    void RefreshOpsListBox()
    {
        OpsListBox.Items.Clear();
        foreach (var g in _opsOverlay.GroupBy(o => o.Op))
            OpsListBox.Items.Add($"{g.Key} × {g.Count()} (placed {g.Count(x => x.Placed)})");
        if (OpsListBox.Items.Count == 0)
            OpsListBox.Items.Add("无工序");
    }

    void RefreshCamFrames()
    {
        _camFrames = CamSimulator.ExpandFrames(_opsOverlay);
        _camFrameIndex = _camFrames.Count == 0
            ? 0
            : Math.Clamp(_camFrameIndex, 0, _camFrames.Count - 1);
        RefreshCamMeta();
    }

    void RefreshCamMeta()
    {
        var frame = _camFrames.Count == 0 ? null : _camFrames[_camFrameIndex];
        CamSimMeta.Text = CamSimulator.Describe(frame, _camFrameIndex, _camFrames.Count);
    }

    void StepCam(int delta)
    {
        if (_camFrames.Count == 0)
        {
            _camTimer.Stop();
            CamPlayBtn.Content = "播放";
            return;
        }
        _camFrameIndex = CamSimulator.Step(_camFrameIndex, _camFrames.Count, delta);
        RefreshCamMeta();
        CanvasHost.InvalidateVisual();
    }

    void OnCamPrevClick(object sender, RoutedEventArgs e) => StepCam(-1);

    void OnCamNextClick(object sender, RoutedEventArgs e) => StepCam(1);

    void OnCamPlayClick(object sender, RoutedEventArgs e)
    {
        if (_camTimer.IsEnabled)
        {
            _camTimer.Stop();
            CamPlayBtn.Content = "播放";
        }
        else
        {
            _camTimer.Start();
            CamPlayBtn.Content = "暂停";
        }
    }

    void OnCamOffsetChanged(object sender, RoutedEventArgs e)
    {
        RebuildOpsOverlay();
        RegenerateNcFromCurrentOps();
        CanvasHost.InvalidateVisual();
    }

    void OnNestVerifyPolyClick(object sender, RoutedEventArgs e)
    {
        if (_session.Package is null || _nest is not { Ok: true })
        {
            SetStatus("多边形校验：无排版");
            return;
        }
        var places = CurrentNestPlacements();
        var hits = NestValidator.FindPolygonCollisions(
            _session.Package.Panels,
            places,
            ParseMm(NestSpacingBox.Text, 12));
        var msg = hits.Count == 0
            ? "Clipper2 多边形 + 间距校验通过"
            : $"发现 {hits.Count} 处多边形/间距冲突：\n" +
              string.Join("\n", hits.Take(20).Select(h => $"{h.PanelIdA} × {h.PanelIdB} · S{h.SheetIndex + 1}"));
        SetStatus(msg.Replace("\n", " · "));
        MessageBox.Show(this, msg, hits.Count == 0 ? "排版校验通过" : "排版校验失败",
            MessageBoxButton.OK, hits.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        CanvasHost.InvalidateVisual();
    }

    List<NestPlacement> CurrentNestPlacements() =>
        _nest?.Placements.Select(p => new NestPlacement
        {
            PanelId = p.PanelId,
            SheetIndex = p.SheetIndex,
            OffsetX = p.OffsetX,
            OffsetY = p.OffsetY,
            RotationDeg = p.RotationDeg,
        }).ToList() ?? [];

    void RefreshPreflightMeta()
    {
        if (_opsOverlay.Count == 0)
        {
            PreflightMeta.Text = "";
            return;
        }
        var report = RunPreflight();
        PreflightMeta.Text = NcPreflight.Format(report);
        PreflightMeta.Foreground = report.Ok
            ? new SolidColorBrush(Color.FromRgb(0x88, 0xCC, 0x88))
            : new SolidColorBrush(Color.FromRgb(0xE0, 0x88, 0x88));
    }

    PreflightReport RunPreflight()
    {
        var profile = ActiveProfileForCam();
        return NcPreflight.Check(
            _opsOverlay,
            profile,
            ParseMm(StockWidthBox.Text, 1220),
            ParseMm(StockLengthBox.Text, 2440));
    }

    HashSet<string> CurrentConflicts()
    {
        if (_session.Package is null || _nest is not { Ok: true }) return [];
        var places = _nest.Placements.Select(p => new NestPlacement
        {
            PanelId = p.PanelId,
            SheetIndex = p.SheetIndex,
            OffsetX = p.OffsetX,
            OffsetY = p.OffsetY,
            RotationDeg = p.RotationDeg,
        }).ToList();
        var hits = NestValidator.FindPolygonCollisions(
            _session.Package.Panels,
            places,
            ParseMm(NestSpacingBox.Text, 12));
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var h in hits)
        {
            set.Add(h.PanelIdA);
            set.Add(h.PanelIdB);
        }
        return set;
    }

    void SyncNestSettingsFromPackage()
    {
        if (_session.Package is null) return;
        var sheet = _session.Package.Sheets.FirstOrDefault();
        if (sheet is not null)
        {
            if (sheet.WidthMm > 0) StockWidthBox.Text = sheet.WidthMm.ToString("0.###");
            if (sheet.LengthMm > 0) StockLengthBox.Text = sheet.LengthMm.ToString("0.###");
            if (sheet.MarginMm > 0) NestBorderBox.Text = sheet.MarginMm.ToString("0.###");
            var gap = sheet.PartClearanceMm > 0 ? sheet.PartClearanceMm : sheet.KerfMm;
            if (gap > 0) NestSpacingBox.Text = gap.ToString("0.###");
        }
    }

    void RefreshNestReport()
    {
        NestUnplacedList.Items.Clear();
        if (_nest is not { Ok: true })
        {
            NestReportMeta.Text = "尚未排版 — 进入密排或点应用并重排";
            return;
        }

        var sw = ParseMm(StockWidthBox.Text, 1220);
        var sh = ParseMm(StockLengthBox.Text, 2440);
        double used = 0;
        if (_session.Package is not null)
        {
            var placed = _nest.Placements.Select(p => p.PanelId).ToHashSet();
            foreach (var p in _session.Package.Panels.Where(p => placed.Contains(p.PanelId)))
            {
                var (w, h) = SizeOf(p);
                used += w * h;
            }
        }
        var sheets = Math.Max(1, _nest.SheetCount);
        var sheetArea = sw * sh * sheets;
        var util = sheetArea > 0 ? used / sheetArea * 100 : 0;
        var gateNote = "";
        if (_session.Package is not null)
        {
            var gate = NestExportGate.Check(
                _session.Package.Panels,
                CurrentNestPlacements(),
                ParseMm(NestSpacingBox.Text, 12));
            gateNote = gate.Ok ? "export_gate: OK" : $"export_gate: FAIL ({gate.Errors.Count})";
        }
        NestReportMeta.Text =
            $"engine: {_nest.Engine}\n" +
            $"util: {util:0.0}%\n" +
            $"area: {used / 1e6:0.000} m2 / sheet {sheetArea / 1e6:0.000} m2 x{sheets}\n" +
            $"placed: {_nest.Placements.Count} · unplaced: {_nest.Unplaced.Count}\n" +
            $"warnings: {_nest.Warnings.Count}\n" +
            gateNote;

        if (_nest.Unplaced.Count == 0 && _nest.Warnings.Count == 0)
            NestUnplacedList.Items.Add("无未排件 · 无警告");
        else
        {
            foreach (var id in _nest.Unplaced)
                NestUnplacedList.Items.Add($"未排 {id}");
            foreach (var w in _nest.Warnings.Take(20))
                NestUnplacedList.Items.Add($"{w.Code}: {w.Message}");
        }
    }


    static double ParseMm(string? text, double fallback) =>
        double.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 0
            ? v
            : fallback;

    void TryLoadDemoPackage() => OnLoadDemoClick(this, new RoutedEventArgs());

    void OnLoadDemoClick(object sender, RoutedEventArgs e)
    {
        var demo = FindDemoPackage();
        if (demo is null)
        {
            SetStatus("示例包未找到 · public/samples/demo_woodjob_120.zip");
            ShowImportDialog(false, "打开示例", "demo_woodjob_120.zip", null, "示例包未找到（public/samples）");
            return;
        }
        var result = _session.OpenPackageFile(demo);
        if (!result.Ok)
        {
            SetStatus("Demo package failed: " + string.Join("; ", result.Errors.Select(err => err.Message)));
            ShowImportDialog(false, "打开示例", Path.GetFileName(demo), result);
            return;
        }
        _nest = null;
        _showNest = false;
        NcPreview.Text = "";
        _module = "production";
        HighlightModule();
        ApplyModuleVisibility();
        BindPackage();
        _stageChanging = true;
        StageTabs.SelectedIndex = 0;
        _stage = "load";
        _stageChanging = false;
        ApplyStageVisibility();
        UpdateStageChrome();
        UpdateCanvasHint();
        RefreshWorkflowDots();
        SetStatus($"已载入示例 · panels={_session.Package!.Panels.Count} · warnings={result.Warnings.Count}");
        ShowImportDialog(true, "打开示例", Path.GetFileName(demo), result);
    }

    static string? FindDemoPackage()
    {
        var walk = AppContext.BaseDirectory;
        for (var i = 0; i < 12; i++)
        {
            foreach (var rel in new[]
                     {
                         Path.Combine("public", "samples", "demo_woodjob_120.zip"),
                         Path.Combine("public", "samples", "demo_cut_package.json"),
                     })
            {
                var p = Path.Combine(walk, rel);
                if (File.Exists(p)) return p;
                var alt = Path.GetFullPath(Path.Combine(walk, "..", "..", "..", "..", "..", rel));
                if (File.Exists(alt)) return alt;
            }
            var parent = Directory.GetParent(walk);
            if (parent is null) break;
            walk = parent.FullName;
        }
        return null;
    }

    void BindPackage()
    {
        PartList.Items.Clear();
        WarnList.Items.Clear();
        if (_session.Package is null)
        {
            PackageMeta.Text = "尚未加载";
            RefreshEmptyState();
            ApplyStageVisibility();
            RefreshWorkflowDots();
            return;
        }
        foreach (var panel in _session.Package.Panels)
            PartList.Items.Add(panel);
        if (PartList.Items.Count > 0)
            PartList.SelectedIndex = 0;
        PackageMeta.Text =
            $"{_session.Package.SchemaName} v{_session.Package.Version}\n" +
            $"job={_session.Package.JobId ?? "—"} · panels={_session.Package.Panels.Count} · sheets={_session.Package.Sheets.Count}";
        foreach (var w in _session.LastWarnings.Take(20))
            WarnList.Items.Add($"{w.Code}: {w.Message}");
        SyncNestSettingsFromPackage();
        RefreshNestReport();
        RefreshGeomRail();
        RefreshWorkflowDots();
        RefreshEmptyState();
        ApplyStageVisibility();
        RefreshMaterialsModule();
        CanvasHost.InvalidateVisual();
    }

    void RefreshGeomRail()
    {
        FeatList.Items.Clear();
        if (_selected is null)
        {
            GeomMeta.Text = "选板后可编辑";
            InspKind.Text = "未选特征";
            DirtyBanner.Visibility = Visibility.Collapsed;
            SmallPanelWarn.Visibility = Visibility.Collapsed;
            return;
        }
        var box = PanelEdit.BBox(_selected);
        var orient = _selected.Orientation;
        GeomMeta.Text =
            $"{_selected.PanelId}" +
            (string.IsNullOrEmpty(_selected.Identity?.ModuleId) ? "" : $" · mod={_selected.Identity!.ModuleId}") + "\n" +
            $"{box.W:0.#} × {box.H:0.#} × {_selected.ThicknessMm:0.#} mm\n" +
            $"材料={_selected.Material ?? "—"} · 面={orient?.MillingFace ?? _selected.Side ?? "—"} · 木纹={_selected.GrainDirection ?? "—"}\n" +
            $"features: {_selected.Features.Count} · 画布拖拽编辑";
        DirtyBanner.Text = _session.ManufacturingDirty
            ? "Nest/CAM 已失效 — 请重新密排后再导出"
            : "";
        DirtyBanner.Visibility = _session.ManufacturingDirty ? Visibility.Visible : Visibility.Collapsed;
        if (PanelEdit.IsSmallPanel(_selected, out var smallReason))
        {
            SmallPanelWarn.Text = $"小板警告：{smallReason}";
            SmallPanelWarn.Visibility = Visibility.Visible;
        }
        else
        {
            SmallPanelWarn.Text = "";
            SmallPanelWarn.Visibility = Visibility.Collapsed;
        }
        foreach (var f in _selected.Features)
        {
            if (PanelEdit.IsHole(f))
                FeatList.Items.Add($"{f.FeatureId} hole D{f.DiameterMm:0.#} @ ({f.X:0.#},{f.Y:0.#}) d={f.DepthMm:0.#}");
            else if (PanelEdit.IsGroove(f))
                FeatList.Items.Add($"{f.FeatureId} groove pts={f.Path?.Count ?? 0} w={f.WidthMm:0.#} d={f.DepthMm:0.#}");
            else
                FeatList.Items.Add($"{f.FeatureId} {f.Kind}");
        }
        if (FeatList.Items.Count == 0)
            FeatList.Items.Add("无特征（仅外轮廓）");
        if (FeatList.Items.Count > 0 && FeatList.SelectedIndex < 0)
            FeatList.SelectedIndex = 0;
        else
            LoadInspectorFromSelection();
    }

    void OnFeatListChanged(object sender, SelectionChangedEventArgs e) => LoadInspectorFromSelection();

    PanelFeature? SelectedFeature()
    {
        if (_selected is null || FeatList.SelectedIndex < 0) return null;
        if (FeatList.SelectedIndex >= _selected.Features.Count) return null;
        return _selected.Features[FeatList.SelectedIndex];
    }

    void LoadInspectorFromSelection()
    {
        var f = SelectedFeature();
        if (f is null)
        {
            InspKind.Text = "未选特征";
            InspXBox.Text = InspYBox.Text = InspDiaBox.Text = InspDepthBox.Text = InspWidthBox.Text = "";
            return;
        }
        InspKind.Text = $"{f.FeatureId} · {f.Kind}";
        InspXBox.Text = f.X.ToString("0.###");
        InspYBox.Text = f.Y.ToString("0.###");
        InspDiaBox.Text = f.DiameterMm?.ToString("0.###") ?? "";
        InspDepthBox.Text = f.DepthMm?.ToString("0.###") ?? "";
        InspWidthBox.Text = f.WidthMm?.ToString("0.###") ?? "";
    }

    void OnInspectApplyClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        var f = SelectedFeature();
        if (f is null)
        {
            SetStatus("先选择特征");
            return;
        }
        double? ParseOpt(string t) => double.TryParse(t, out var v) ? v : null;
        var next = PanelEdit.UpdateFeatureParams(
            _selected,
            f.FeatureId,
            x: ParseOpt(InspXBox.Text),
            y: ParseOpt(InspYBox.Text),
            diameterMm: ParseOpt(InspDiaBox.Text),
            depthMm: ParseOpt(InspDepthBox.Text),
            widthMm: ParseOpt(InspWidthBox.Text));
        var idx = FeatList.SelectedIndex;
        CommitPanel(next);
        if (idx >= 0 && idx < FeatList.Items.Count)
            FeatList.SelectedIndex = idx;
    }

    void OnUndoClick(object sender, RoutedEventArgs e)
    {
        if (_session.TryUndo()) AfterHistoryRestore();
        else SetStatus("没有可撤销的编辑");
    }

    void OnRedoClick(object sender, RoutedEventArgs e)
    {
        if (_session.TryRedo()) AfterHistoryRestore();
        else SetStatus("没有可重做的编辑");
    }

    void CommitPanel(PanelPart next)
    {
        _session.ReplacePanel(next);
        _selected = next;
        InvalidateManufacturingOutputs("geom write-back");
        // refresh list item reference
        var idx = PartList.SelectedIndex;
        PartList.Items.Clear();
        if (_session.Package is not null)
            foreach (var p in _session.Package.Panels)
                PartList.Items.Add(p);
        if (idx >= 0 && idx < PartList.Items.Count)
            PartList.SelectedIndex = idx;
        RefreshGeomRail();
        RefreshNestReport();
        UpdateCanvasHint();
        CanvasHost.InvalidateVisual();
    }

    void InvalidateManufacturingOutputs(string reason)
    {
        _nest = null;
        _opsOverlay = [];
        _showNest = _stage is "nest" or "ops";
        NcPreview.Text = "";
        SetStatus($"已编辑 · Nest/CAM 已失效（{reason}）· 请重新密排");
        RefreshWorkflowDots();
        RefreshOneClickExport();
    }

    void OnGeomMoveClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        CommitPanel(PanelEdit.TranslateFeatures(_selected, 10, 0));
        SetStatus($"特征右移 10mm · {_selected.PanelId}");
    }

    void OnGeomRotClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        CommitPanel(PanelEdit.RotatePanel(_selected, 90));
        SetStatus($"旋转 90° · {_selected.PanelId}");
    }

    void OnGeomHoleClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        var box = PanelEdit.BBox(_selected);
        CommitPanel(PanelEdit.AddVerticalHole(_selected, box.MinX + box.W / 2, box.MinY + box.H / 2));
        SetStatus($"已加孔 · {_selected.PanelId}");
    }

    void OnGeomGrooveClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        var box = PanelEdit.BBox(_selected);
        var y = box.MinY + box.H * 0.25;
        CommitPanel(PanelEdit.AddVerticalGroove(_selected, [new Point2(box.MinX, y), new Point2(box.MaxX, y)]));
        SetStatus($"已加槽 · {_selected.PanelId}");
    }

    void OnGeomMirrorXClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        CommitPanel(PanelEdit.Mirror(_selected, "X"));
        SetStatus($"镜像 X · {_selected.PanelId}");
    }

    void OnGeomMirrorYClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        CommitPanel(PanelEdit.Mirror(_selected, "Y"));
        SetStatus($"镜像 Y · {_selected.PanelId}");
    }

    void OnGeomDuplicateClick(object sender, RoutedEventArgs e) => DuplicateSelectedPanel();

    void OnPastePanelClick(object sender, RoutedEventArgs e) => PasteClipboardPanel();

    void OnCutPanelClick(object sender, RoutedEventArgs e) => CutSelectedPanel();

    void OnGeomDeleteFeatureClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        var f = SelectedFeature();
        if (f is null)
        {
            SetStatus("先选择要删除的特征");
            return;
        }
        CommitPanel(PanelEdit.RemoveFeature(_selected, f.FeatureId));
        SetStatus($"已删特征 {f.FeatureId}");
    }

    void OnDeletePanelClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null || _session.Package is null) return;
        var id = _selected.PanelId;
        _session.RemovePanel(id);
        InvalidateManufacturingOutputs("delete panel");
        RefreshPartList(selectId: null);
        SetStatus($"已删除板件 {id}");
    }

    void CopySelectedToClipboard()
    {
        if (_selected is null) return;
        _clipboardPanel = PanelEdit.Duplicate(_selected, _selected.PanelId);
        SetStatus($"已复制 {_selected.PanelId}（Ctrl+V 粘贴）");
    }

    void CutSelectedPanel()
    {
        if (_selected is null) return;
        CopySelectedToClipboard();
        OnDeletePanelClick(this, new RoutedEventArgs());
    }

    void PasteClipboardPanel()
    {
        if (_clipboardPanel is null || _session.Package is null)
        {
            SetStatus("剪贴板为空");
            return;
        }
        var id = _session.NextCopyPanelId(StripCopySuffix(_clipboardPanel.PanelId));
        var copy = PanelEdit.Duplicate(_clipboardPanel, id);
        _session.ReplacePanel(copy);
        InvalidateManufacturingOutputs("paste panel");
        RefreshPartList(selectId: id);
        SetStatus($"已粘贴 {id}");
    }

    void DuplicateSelectedPanel()
    {
        if (_selected is null || _session.Package is null) return;
        CopySelectedToClipboard();
        var id = _session.NextCopyPanelId(_selected.PanelId);
        var copy = PanelEdit.Duplicate(_selected, id);
        _session.ReplacePanel(copy);
        InvalidateManufacturingOutputs("duplicate panel");
        RefreshPartList(selectId: id);
        SetStatus($"已复制为 {id}");
    }

    static string StripCopySuffix(string id)
    {
        var idx = id.IndexOf("_copy", StringComparison.OrdinalIgnoreCase);
        return idx > 0 ? id[..idx] : id;
    }

    void RefreshPartList(string? selectId)
    {
        PartList.Items.Clear();
        if (_session.Package is not null)
            foreach (var p in _session.Package.Panels)
                PartList.Items.Add(p);
        _selected = PartList.Items.OfType<PanelPart>().FirstOrDefault(p => p.PanelId == selectId)
            ?? PartList.Items.OfType<PanelPart>().FirstOrDefault();
        if (_selected is not null)
            PartList.SelectedItem = _selected;
        RefreshGeomRail();
        RefreshNestReport();
        UpdateCanvasHint();
        CanvasHost.InvalidateVisual();
    }

    void OnLockPlaceClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null || _nest is not { Ok: true })
        {
            SetStatus("无摆位可锁定");
            return;
        }
        var id = _selected.PanelId;
        if (!_nest.Placements.Any(p => p.PanelId == id))
        {
            SetStatus("选中板未排版");
            return;
        }
        if (_locked.Contains(id))
        {
            _locked.Remove(id);
            LockPlaceBtn.Content = "锁定摆位";
            SetStatus($"已解锁摆位 · {id}");
        }
        else
        {
            _locked.Add(id);
            LockPlaceBtn.Content = "解锁摆位";
            SetStatus($"已锁定摆位 · {id}");
        }
        CanvasHost.InvalidateVisual();
    }

    async void OnApplyNestSettingsClick(object sender, RoutedEventArgs e) =>
        await RunNestAsync(withNc: false);

    async void OnNestClick(object sender, RoutedEventArgs e) =>
        await RunNestAsync(withNc: true);

    async Task RunNestAsync(bool withNc)
    {
        if (_nestBusy) return;
        if (_session.Package is null)
        {
            SetStatus("请先载入方案");
            return;
        }

        _nestBusy = true;
        try
        {
            var allowRot = NestAllowRotChk.IsChecked == true;
            var border = ParseMm(NestBorderBox.Text, 15);
            var spacing = ParseMm(NestSpacingBox.Text, 12);
            var settings = new NestSettings
            {
                MarginMm = border,
                ClearanceMm = spacing,
                AllowRotation = allowRot,
                GrainLock = true,
                PreferLockedPlacements = true,
            };
            var consistency = settings.ValidateConsistency();
            if (consistency.Count > 0)
                SetStatus("Nest settings warn: " + string.Join(", ", consistency));

            var sheets = BuildNestSheetQueue(border);
            var prevPlaces = _nest?.Placements.ToDictionary(p => p.PanelId, p => p);

            var packed = GroupedBlfNester.Pack(
                _session.Package.Panels,
                settings,
                sheets,
                SizeOf);

            _nest = new StartNestingReply
            {
                Ok = true,
                Engine = packed.Engine,
                SheetCount = packed.SheetCount,
            };
            _nest.Unplaced.AddRange(packed.Unplaced);
            foreach (var p in packed.Placements)
            {
                _nest.Placements.Add(new NestPlacementMsg
                {
                    PanelId = p.PanelId,
                    SheetIndex = p.SheetIndex,
                    OffsetX = p.OffsetX,
                    OffsetY = p.OffsetY,
                    RotationDeg = p.RotationDeg,
                });
            }
            foreach (var r in packed.UnplacedReasons)
            {
                _nest.Warnings.Add(new NestWarningMsg
                {
                    Code = r.Code,
                    Message = $"{r.PanelId}: {r.Message}",
                    PanelIdA = r.PanelId,
                });
            }
            foreach (var g in packed.GroupReports)
            {
                _nest.Warnings.Add(new NestWarningMsg
                {
                    Code = "group_report",
                    Message =
                        $"{g.Key}: placed {g.PlacedCount}/{g.PartCount} · sheets {g.SheetCount} · util {g.UtilizationPct:0.0}%",
                });
            }
            if (prevPlaces is not null && _locked.Count > 0)
            {
                foreach (var place in _nest.Placements)
                {
                    if (!_locked.Contains(place.PanelId)) continue;
                    if (!prevPlaces.TryGetValue(place.PanelId, out var old)) continue;
                    place.OffsetX = old.OffsetX;
                    place.OffsetY = old.OffsetY;
                    place.RotationDeg = old.RotationDeg;
                    place.SheetIndex = old.SheetIndex;
                }
            }

            var collisions = NestValidator.FindPolygonCollisions(
                _session.Package.Panels,
                CurrentNestPlacements(),
                spacing);
            foreach (var c in collisions)
            {
                _nest.Warnings.Add(new NestWarningMsg
                {
                    Code = "poly_gap",
                    Message = $"polygon spacing/collision {c.PanelIdA} × {c.PanelIdB} on sheet {c.SheetIndex}",
                    PanelIdA = c.PanelIdA,
                    PanelIdB = c.PanelIdB,
                    SheetIndex = c.SheetIndex,
                });
            }

            var gate = NestExportGate.Check(
                _session.Package.Panels,
                CurrentNestPlacements(),
                spacing);
            if (!gate.Ok)
            {
                foreach (var err in gate.Errors.Take(12))
                {
                    _nest.Warnings.Add(new NestWarningMsg
                    {
                        Code = "export_gate",
                        Message = err,
                    });
                }
            }

            _showNest = true;
            if (_stage != "nest" && _stage != "ops")
            {
                _stageChanging = true;
                StageTabs.SelectedIndex = 2;
                _stage = "nest";
                _stageChanging = false;
            }
            ApplyStageVisibility();
            UpdateCanvasHint();
            RebuildOpsOverlay();
            _session.MarkManufacturingClean();

            var opsNote = "";
            var ncNote = "";
            if (withNc)
            {
                try
                {
                    var profile = ActiveProfileForCam();
                    var opsForNc = _opsOverlay.Select(o =>
                        o.Op == "contour" ? o with { DepthMm = profile.ContourDepthMm } : o).ToList();
                    var nc = NcEmitter.OpsToNc(opsForNc, profile);
                    NcPreview.Text = nc;
                    ncNote = $" · NC {profile.Id} lines={nc.Split('\n').Length}";
                    opsNote = $" · ops c={opsForNc.Count(o => o.Op == "contour")} d={opsForNc.Count(o => o.Op == "drill")} g={opsForNc.Count(o => o.Op == "groove")}";
                }
                catch (Exception ex)
                {
                    opsNote = " · ops/nc err: " + ex.Message;
                    NcPreview.Text = "// " + ex.Message;
                }
            }

            var warn = _nest.Warnings.Count;
            var warnTxt = warn == 0
                ? " · validate ok"
                : $" · WARN {warn}: " + string.Join("; ", _nest.Warnings.Take(3).Select(w => w.Message));
            SetStatus(
                $"Nest {_nest.Engine} · placed={_nest.Placements.Count} sheets={_nest.SheetCount} unplaced={_nest.Unplaced.Count}{warnTxt}{opsNote}{ncNote}");
            RefreshNestReport();
            RebuildOpsOverlay();
            RefreshWorkflowDots();
            CanvasHost.InvalidateVisual();
            await RefreshWorkerAsync(); // keep worker warm; nest is local
        }
        catch (Exception ex)
        {
            SetStatus("Nest error: " + ex.Message);
        }
        finally
        {
            _nestBusy = false;
        }
    }

    List<NestSheetSpec> BuildNestSheetQueue(double border)
    {
        var queue = new List<NestSheetSpec>();
        var pkgSheets = _session.Package?.Sheets ?? [];
        if (pkgSheets.Count > 0)
        {
            foreach (var s in pkgSheets)
            {
                queue.Add(new NestSheetSpec
                {
                    WidthMm = s.WidthMm > 0 ? s.WidthMm : ParseMm(StockWidthBox.Text, 1220),
                    LengthMm = s.LengthMm > 0 ? s.LengthMm : ParseMm(StockLengthBox.Text, 2440),
                    BorderMm = s.MarginMm > 0 ? s.MarginMm : border,
                    Label = s.SheetId,
                    Material = s.Material,
                    ThicknessMm = s.ThicknessMm,
                    Blocked = s.DefectRegions.Select(d => new NestBlockedRect
                    {
                        MinX = d.MinX, MinY = d.MinY, MaxX = d.MaxX, MaxY = d.MaxY,
                    }).ToList(),
                });
            }
        }
        else
        {
            // Blank template (ThicknessMm=0): GroupedBlfNester clones per material/thickness group.
            queue.Add(new NestSheetSpec
            {
                WidthMm = ParseMm(StockWidthBox.Text, 1220),
                LengthMm = ParseMm(StockLengthBox.Text, 2440),
                BorderMm = border,
                Label = "STOCK",
                Material = null,
                ThicknessMm = 0,
            });
        }

        foreach (var r in _library.Remnants.Where(x => x.UseInNest && x.WidthMm > 0 && x.LengthMm > 0))
        {
            queue.Add(new NestSheetSpec
            {
                WidthMm = r.WidthMm,
                LengthMm = r.LengthMm,
                BorderMm = Math.Min(border, 8),
                Label = r.Id,
                Material = r.Material,
                ThicknessMm = r.ThicknessMm,
            });
        }
        return queue;
    }

    MachineProfile ActiveProfileForCam()
    {
        var p = MachineCatalog.Get(SelectedMachineId());
        var tool = _library.Tools.FirstOrDefault(t => t.Id == _activeToolId);
        return new MachineProfile
        {
            Id = p.Id,
            Name = p.Name,
            Dialect = p.Dialect,
            ProgramEnd = p.ProgramEnd,
            SafeZMm = p.SafeZMm,
            FeedXyMmMin = tool?.FeedXyMmMin > 0 ? tool.FeedXyMmMin : p.FeedXyMmMin,
            FeedZMmMin = tool?.FeedZMmMin > 0 ? tool.FeedZMmMin : p.FeedZMmMin,
            SpindleRpm = tool?.SpindleRpm > 0 ? tool.SpindleRpm : p.SpindleRpm,
            ToolDiameterMm = tool?.DiameterMm > 0 ? tool.DiameterMm : p.ToolDiameterMm,
            ContourDepthMm = p.ContourDepthMm,
            ContourStepdownMm = p.ContourStepdownMm,
            DrillPeckMm = p.DrillPeckMm,
            EnableContour = _enableContour,
            EnableDrill = _enableDrill,
            EnableGroove = _enableGroove,
            OriginNote = p.OriginNote,
        };
    }

    void RegenerateNcFromCurrentOps()
    {
        if (_opsOverlay.Count == 0 || _nest is not { Ok: true }) return;
        var profile = ActiveProfileForCam();
        var ops = _opsOverlay.Select(o =>
            o.Op == "contour" ? o with { DepthMm = profile.ContourDepthMm } : o);
        NcPreview.Text = NcEmitter.OpsToNc(ops, profile);
        RefreshWorkflowDots();
        RefreshPreflightMeta();
    }

    async void OnOpenClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "WoodJob / cut-package|*.zip;*.json;manifest.json|WoodJob zip (*.zip)|*.zip|Cut package (*.json)|*.json|All|*.*",
            Title = "Open cabinetnc.woodjob or cut-package",
        };
        if (dlg.ShowDialog() != true) return;
        var result = _session.OpenPackageFile(dlg.FileName);
        if (!result.Ok)
        {
            SetStatus("Import failed: " + string.Join("; ", result.Errors.Select(x => $"{x.Path}: {x.Message}")));
            ShowImportDialog(false, "载入方案", Path.GetFileName(dlg.FileName), result);
            return;
        }
        _nest = null;
        _showNest = false;
        NcPreview.Text = "";
        _module = "production";
        HighlightModule();
        ApplyModuleVisibility();
        BindPackage();
        _stageChanging = true;
        StageTabs.SelectedIndex = 0;
        _stage = "load";
        _stageChanging = false;
        ApplyStageVisibility();
        UpdateStageChrome();
        SetStatus($"Opened {Path.GetFileName(dlg.FileName)} · panels={_session.Package!.Panels.Count} · {_session.Package.SchemaName}");
        ShowImportDialog(true, "载入方案", Path.GetFileName(dlg.FileName), result);
        await RefreshWorkerAsync();
    }


    void OnOpenProjectClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "CabinetNC project|project.db;*.db|All|*.*",
            Title = "Open project.db",
        };
        if (dlg.ShowDialog() != true) return;
        var doc = _store.Load(dlg.FileName);
        if (doc is null)
        {
            SetStatus("Project empty or unreadable");
            ShowImportDialog(false, "打开工程", Path.GetFileName(dlg.FileName), null, "工程为空或无法读取");
            return;
        }
        var result = _session.OpenPackageJson(doc.PackageJson, dlg.FileName);
        if (!result.Ok)
        {
            SetStatus("Package in project invalid: " + string.Join("; ", result.Errors.Select(x => x.Message)));
            ShowImportDialog(false, "打开工程", Path.GetFileName(dlg.FileName), result);
            return;
        }
        _session.MachineId = doc.MachineId;
        _session.SetProjectDbPath(dlg.FileName);
        MachineCombo.SelectedValue = doc.MachineId;
        NcPreview.Text = doc.NcText ?? "";

        var places = SqliteProjectStore.DeserializeNest(doc.NestPlacementsJson);
        if (places.Count > 0)
        {
            _nest = new StartNestingReply { Ok = true, Engine = "restored", SheetCount = places.Max(p => p.SheetIndex) + 1 };
            foreach (var p in places)
            {
                _nest.Placements.Add(new NestPlacementMsg
                {
                    PanelId = p.PanelId,
                    SheetIndex = p.SheetIndex,
                    OffsetX = p.OffsetX,
                    OffsetY = p.OffsetY,
                    RotationDeg = p.RotationDeg,
                });
            }
            _showNest = true;
            _stageChanging = true;
            StageTabs.SelectedIndex = 2; // nest
            _stage = "nest";
            _stageChanging = false;
        }
        else
        {
            _nest = null;
            _showNest = false;
            _stageChanging = true;
            StageTabs.SelectedIndex = 0;
            _stage = "load";
            _stageChanging = false;
        }

        _module = "production";
        HighlightModule();
        ApplyModuleVisibility();
        BindPackage();
        ApplyStageVisibility();
        UpdateStageChrome();
        SetStatus($"Opened project · panels={_session.Package!.Panels.Count} · nest={places.Count} · {doc.Name}");
        ShowImportDialog(true, "打开工程", Path.GetFileName(dlg.FileName), result,
            $"工程名: {doc.Name}\n机型: {doc.MachineId}\n已恢复摆位: {places.Count}");
    }

    /// <summary>Import result popup — success/fail + basic package stats.</summary>
    void ShowImportDialog(bool ok, string action, string sourceLabel, PackageImportResult? result, string? extra = null)
    {
        var sb = new StringBuilder();
        if (ok && _session.Package is { } pkg)
        {
            sb.AppendLine($"{action}成功");
            sb.AppendLine();
            sb.AppendLine($"文件: {sourceLabel}");
            sb.AppendLine($"格式: {pkg.SchemaName}  v{pkg.Version}");
            if (!string.IsNullOrEmpty(pkg.JobId))
                sb.AppendLine($"Job: {pkg.JobId}");
            sb.AppendLine($"单位: {pkg.Units}");
            sb.AppendLine($"板件: {pkg.Panels.Count}");
            sb.AppendLine($"板材规格: {pkg.Sheets.Count}");
            sb.AppendLine($"特征合计: {pkg.Panels.Sum(p => p.Features.Count)}");
            var mats = pkg.Panels.Select(p => p.Material).Where(m => !string.IsNullOrEmpty(m)).Distinct().Count();
            if (mats > 0) sb.AppendLine($"材料种类: {mats}");
            if (result is { Warnings.Count: > 0 })
            {
                sb.AppendLine();
                sb.AppendLine($"警告 ({result.Warnings.Count}):");
                foreach (var w in result.Warnings.Take(6))
                    sb.AppendLine($"  · {w.Message}");
                if (result.Warnings.Count > 6)
                    sb.AppendLine($"  …另有 {result.Warnings.Count - 6} 条");
            }
            if (!string.IsNullOrWhiteSpace(extra))
            {
                sb.AppendLine();
                sb.AppendLine(extra.Trim());
            }
            MessageBox.Show(this, sb.ToString().TrimEnd(), $"{action}成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            sb.AppendLine($"{action}失败");
            sb.AppendLine();
            sb.AppendLine($"文件: {sourceLabel}");
            if (result is { Errors.Count: > 0 })
            {
                sb.AppendLine();
                sb.AppendLine("错误:");
                foreach (var err in result.Errors.Take(10))
                    sb.AppendLine($"  · [{err.Path}] {err.Message}");
                if (result.Errors.Count > 10)
                    sb.AppendLine($"  …另有 {result.Errors.Count - 10} 条");
            }
            if (result is { Warnings.Count: > 0 })
            {
                sb.AppendLine();
                sb.AppendLine($"警告 ({result.Warnings.Count}):");
                foreach (var w in result.Warnings.Take(4))
                    sb.AppendLine($"  · {w.Message}");
            }
            if (!string.IsNullOrWhiteSpace(extra))
            {
                sb.AppendLine();
                sb.AppendLine(extra.Trim());
            }
            MessageBox.Show(this, sb.ToString().TrimEnd(), $"{action}失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void OnSaveProjectClick(object sender, RoutedEventArgs e)
    {
        if (_session.Package is null || string.IsNullOrWhiteSpace(_session.PackageJson))
        {
            SetStatus("Nothing to save — open a cut-package first");
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "CabinetNC project|project.db|SQLite|*.db",
            FileName = Path.GetFileName(_session.ProjectDbPath) ?? "project.db",
            Title = "Save project.db",
        };
        if (!string.IsNullOrEmpty(_session.ProjectDbPath))
            dlg.InitialDirectory = Path.GetDirectoryName(_session.ProjectDbPath);
        if (dlg.ShowDialog() != true) return;

        var nestJson = _nest is { Ok: true }
            ? SqliteProjectStore.SerializeNest(_nest.Placements.Select(p => new NestPlacementDto
            {
                PanelId = p.PanelId,
                SheetIndex = p.SheetIndex,
                OffsetX = p.OffsetX,
                OffsetY = p.OffsetY,
                RotationDeg = p.RotationDeg,
            }))
            : null;

        var name = Path.GetFileNameWithoutExtension(dlg.FileName);
        _store.Save(dlg.FileName, new ProjectDocument
        {
            Name = name,
            PackageJson = _session.PackageJson!,
            MachineId = SelectedMachineId(),
            NestPlacementsJson = nestJson,
            NcText = string.IsNullOrWhiteSpace(NcPreview.Text) || NcPreview.Text.StartsWith("//")
                ? null
                : NcPreview.Text,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        _session.SetProjectDbPath(dlg.FileName);
        _session.MachineId = SelectedMachineId();
        SetStatus($"Saved project → {dlg.FileName}");
    }

    async void OnPingClick(object sender, RoutedEventArgs e) => await RefreshWorkerAsync();

    void OnRouteEnableClick(object sender, RoutedEventArgs e)
    {
        _enableContour = RouteContourChk.IsChecked == true;
        _enableDrill = RouteDrillChk.IsChecked == true;
        _enableGroove = RouteGrooveChk.IsChecked == true;
        RebuildOpsOverlay();
        RegenerateNcFromCurrentOps();
        CanvasHost.InvalidateVisual();
        SetStatus($"工序开关 · contour={_enableContour} drill={_enableDrill} groove={_enableGroove}");
    }

    void OnPreflightClick(object sender, RoutedEventArgs e)
    {
        RebuildOpsOverlay();
        var report = RunPreflight();
        MessageBox.Show(this, NcPreflight.Format(report), report.Ok ? "预检通过" : "预检失败",
            MessageBoxButton.OK, report.Ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    bool GuardExportPreflight()
    {
        if (_session.ManufacturingDirty || _nest is not { Ok: true })
        {
            MessageBox.Show(this,
                "板件已编辑，或尚未完成有效密排。\n请重新密排并生成刀路后再导出。",
                "Nest/CAM 已失效",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        if (_session.Package is not null)
        {
            var clearance = ParseMm(NestSpacingBox.Text, 12);
            var nestGate = NestExportGate.Check(
                _session.Package.Panels,
                CurrentNestPlacements(),
                clearance);
            if (!nestGate.Ok)
            {
                MessageBox.Show(this,
                    "密排间距/碰撞/混组硬门未通过，禁止导出：\n\n" +
                    string.Join("\n", nestGate.Errors.Take(20)),
                    "Nest 导出硬门",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        RebuildOpsOverlay();
        var report = RunPreflight();
        RefreshPreflightMeta();
        if (report.Ok) return true;
        var r = MessageBox.Show(this,
            NcPreflight.Format(report) + "\n\n仍要继续导出吗？",
            "预检未通过",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        return r == MessageBoxResult.Yes;
    }

    void OnExportDxfClick(object sender, RoutedEventArgs e)
    {
        if (_session.Package is null || _nest is not { Ok: true })
        {
            SetStatus("无排版 — 先密排");
            return;
        }
        if (!GuardExportPreflight()) return;
        var dlg = new SaveFileDialog
        {
            Filter = "DXF (*.dxf)|*.dxf|All|*.*",
            FileName = $"{_session.Package.JobId ?? "nest"}_S1.dxf",
            Title = "导出排版 DXF",
        };
        if (dlg.ShowDialog() != true) return;
        var places = _nest.Placements.Select(p => new NestPlacement
        {
            PanelId = p.PanelId,
            SheetIndex = p.SheetIndex,
            OffsetX = p.OffsetX,
            OffsetY = p.OffsetY,
            RotationDeg = p.RotationDeg,
        }).ToList();
        var dxf = NestDxfWriter.Write(_session.Package, places, sheetIndex: 0);
        File.WriteAllText(dlg.FileName, dxf);
        SetStatus($"已导出 DXF → {dlg.FileName}");
    }

    void OnExportJobSheetClick(object sender, RoutedEventArgs e)
    {
        if (_session.Package is null)
        {
            SetStatus("无方案");
            return;
        }
        var places = _nest?.Placements.Select(p => new NestPlacement
        {
            PanelId = p.PanelId,
            SheetIndex = p.SheetIndex,
            OffsetX = p.OffsetX,
            OffsetY = p.OffsetY,
            RotationDeg = p.RotationDeg,
        }).ToList();
        var util = EstimateUtilization();
        var html = JobSheetBuilder.BuildHtml(
            _session.Package,
            ActiveProfileForCam(),
            places,
            _locked,
            NcPreflight.Format(RunPreflight()),
            util,
            _nest?.Unplaced.Count ?? 0);
        var dlg = new SaveFileDialog
        {
            Filter = "HTML (*.html)|*.html|All|*.*",
            FileName = $"{_session.Package.JobId ?? "job"}_sheet.html",
            Title = "导出工单",
        };
        if (dlg.ShowDialog() != true) return;
        File.WriteAllText(dlg.FileName, html);
        SetStatus($"已导出工单 → {dlg.FileName}");
    }

    void OnExportJsonClick(object sender, RoutedEventArgs e)
    {
        if (_session.Package is null || string.IsNullOrWhiteSpace(_session.PackageJson))
        {
            SetStatus("无包可导出");
            return;
        }
        var dlg = new SaveFileDialog
        {
            Filter = "Cut package JSON (*.json)|*.json|All|*.*",
            FileName = $"{_session.Package.JobId ?? "package"}.cut.json",
            Title = "导出 cut-package JSON",
        };
        if (dlg.ShowDialog() != true) return;
        File.WriteAllText(dlg.FileName, _session.PackageJson);
        SetStatus($"已导出 JSON → {dlg.FileName}");
    }

    void OnExportBundleClick(object sender, RoutedEventArgs e)
    {
        if (_session.Package is null || _nest is not { Ok: true })
        {
            SetStatus("需要方案 + 密排");
            return;
        }
        if (!GuardExportPreflight()) return;
        if (!HasNcText())
        {
            SetStatus("无 NC — 先生成加工档");
            return;
        }
        var dlg = new SaveFileDialog
        {
            Filter = "Folder marker|*.txt",
            FileName = "export_here.txt",
            Title = "选择导出目录（保存此标记文件所在文件夹）",
        };
        if (dlg.ShowDialog() != true) return;
        var dir = Path.GetDirectoryName(dlg.FileName)!;
        var baseName = _session.Package.JobId ?? "job";
        var places = _nest.Placements.Select(p => new NestPlacement
        {
            PanelId = p.PanelId,
            SheetIndex = p.SheetIndex,
            OffsetX = p.OffsetX,
            OffsetY = p.OffsetY,
            RotationDeg = p.RotationDeg,
        }).ToList();
        File.WriteAllText(Path.Combine(dir, baseName + ".nc"), NcPreview.Text);
        File.WriteAllText(Path.Combine(dir, baseName + "_S1.dxf"), NestDxfWriter.Write(_session.Package, places, 0));
        File.WriteAllText(Path.Combine(dir, baseName + "_sheet.html"), JobSheetBuilder.BuildHtml(
            _session.Package, ActiveProfileForCam(), places, _locked,
            NcPreflight.Format(RunPreflight()), EstimateUtilization(), _nest.Unplaced.Count));
        if (!string.IsNullOrWhiteSpace(_session.PackageJson))
            File.WriteAllText(Path.Combine(dir, baseName + ".cut.json"), _session.PackageJson);
        try { File.Delete(dlg.FileName); } catch { /* marker optional */ }
        SetStatus($"一键打包完成 → {dir}");
        MessageBox.Show(this, $"已写入:\n{baseName}.nc\n{baseName}_S1.dxf\n{baseName}_sheet.html\n{baseName}.cut.json\n\n目录:\n{dir}",
            "一键打包成功", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    double? EstimateUtilization()
    {
        if (_session.Package is null || _nest is not { Ok: true }) return null;
        var sw = ParseMm(StockWidthBox.Text, 1220);
        var sh = ParseMm(StockLengthBox.Text, 2440);
        double used = 0;
        var placed = _nest.Placements.Select(p => p.PanelId).ToHashSet();
        foreach (var p in _session.Package.Panels.Where(p => placed.Contains(p.PanelId)))
        {
            var (w, h) = SizeOf(p);
            used += w * h;
        }
        var sheetArea = sw * sh * Math.Max(1, _nest.SheetCount);
        return sheetArea > 0 ? used / sheetArea * 100 : null;
    }

    void OnSaveNcClick(object sender, RoutedEventArgs e)
    {
        var text = NcPreview.Text;
        if (string.IsNullOrWhiteSpace(text) || text.StartsWith("//"))
        {
            SetStatus("No NC to save — run Nest + NC first");
            return;
        }
        if (!GuardExportPreflight()) return;
        var dlg = new SaveFileDialog
        {
            Filter = "NC (*.nc)|*.nc|G-code (*.ngc)|*.ngc|All|*.*",
            FileName = $"{SelectedMachineId()}.nc",
            Title = "Save NC",
        };
        if (dlg.ShowDialog() != true) return;
        File.WriteAllText(dlg.FileName, text);
        SetStatus($"Saved NC → {dlg.FileName}");
        _stageChanging = true;
        StageTabs.SelectedIndex = 4;
        _stage = "out";
        _stageChanging = false;
        ApplyStageVisibility();
        UpdateStageChrome();
        RefreshWorkflowDots();
    }

    async void OnOneClickExportClick(object sender, RoutedEventArgs e)
    {
        if (_session.Package is null)
        {
            SetStatus("请先载入方案");
            return;
        }
        if (_nest is not { Ok: true } || !HasNcText())
        {
            SetStatus("一键导出：正在生成密排与加工档…");
            await RunNestAsync(withNc: true);
        }
        if (!HasNcText())
        {
            SetStatus("一键导出失败：无 NC");
            return;
        }
        OnSaveNcClick(sender, e);
    }

    static (double w, double h) SizeOf(PanelPart p)
    {
        var pts = p.Outline.Points;
        if (pts.Count < 2) return (0, 0);
        return (pts.Max(pt => pt.X) - pts.Min(pt => pt.X), pts.Max(pt => pt.Y) - pts.Min(pt => pt.Y));
    }

    async Task RefreshWorkerAsync()
    {
            // encoding-fixed removed: broken string
            SetStatus("updated");
        var ok = await _worker.EnsureStartedAsync();
        if (!ok)
        {
            WorkerBadge.Text = "Worker: DOWN";
            WorkerBadge.Foreground = Brushes.IndianRed;
            SetStatus(_worker.LastError ?? "Worker failed");
            return;
        }

        try
        {
            var client = _worker.GetHealthClient()!;
            var ver = await client.GetWorkerVersionAsync(new());
            var ping = await client.PingAsync(new() { Token = "ui" });
            WorkerBadge.Text = $"Worker: {ver.WorkerVersion} 路 ping ok";
            WorkerBadge.Foreground = Brushes.SeaGreen;
            SetStatus($"Worker ready 路 contract={ver.ContractVersion} 路 machine={SelectedMachineId()} 路 {ping.Message}");
        }
        catch (Exception ex)
        {
            WorkerBadge.Text = "Worker: ERROR";
            WorkerBadge.Foreground = Brushes.IndianRed;
            SetStatus(ex.Message);
        }
    }

    void OnPartSelected(object sender, SelectionChangedEventArgs e)
    {
        _selected = PartList.SelectedItem as PanelPart;
        if (_selected is not null && _locked.Contains(_selected.PanelId))
            LockPlaceBtn.Content = "瑙ｉ攣鎽嗕綅";
        else
            LockPlaceBtn.Content = "閿佸畾鎽嗕綅";
        RefreshGeomRail();
        if (!_showNest || _stage == "load") CanvasHost.InvalidateVisual();
    }

    (float X, float Y) CanvasPixelPos(MouseEventArgs e)
    {
        // 榧犳爣鏄?DIP锛汼kia PaintSurface 鏄墿鐞嗗儚绱?鈥?蹇呴』鍚屼竴鍧愭爣绯诲仛 hit-test
        var pos = e.GetPosition(CanvasHost);
        return ((float)(pos.X * _dpiX), (float)(pos.Y * _dpiY));
    }

    void RefreshDpi()
    {
        var dpi = VisualTreeHelper.GetDpi(CanvasHost);
        _dpiX = dpi.DpiScaleX;
        _dpiY = dpi.DpiScaleY;
    }

    void OnCanvasDown(object sender, MouseButtonEventArgs e)
    {
        RefreshDpi();
        var (x, y) = CanvasPixelPos(e);

        if (_stage == "load" && _selected is not null)
        {
            var w = _surfaceW > 0 ? _surfaceW : Math.Max(1, (int)(CanvasHost.ActualWidth * _dpiX));
            var h = _surfaceH > 0 ? _surfaceH : Math.Max(1, (int)(CanvasHost.ActualHeight * _dpiY));
            var view = GeomInteraction.BuildView(_selected, w, h);
            _geomView = view;
            var hit = GeomInteraction.HitTest(_selected, view, x, y);
            if (hit is null || hit.Value.Type == "panel")
            {
                _dragMode = null;
                SetStatus("几何: 点蓝色孔心 / 红色槽端 / 黑边手柄再拖");
                return;
            }
            _dragMode = "geom";
            _geomHit = hit;
            _geomStart = _selected;
            CanvasPane.CaptureMouse();
            SetStatus($"拖动 {hit.Value.Type}");
            e.Handled = true;
            return;
        }

        if ((_stage is "nest" or "ops") && _nest is { Ok: true } && _session.Package is not null)
        {
            EnsureNestViewMetrics();
            var hitId = HitTestNest(x, y);
            if (hitId is null)
            {
                SetStatus("未点中板件");
                return;
            }
            var place = _nest.Placements.FirstOrDefault(p => p.PanelId == hitId);
            if (place is null) return;
            for (var i = 0; i < PartList.Items.Count; i++)
            {
                if (PartList.Items[i] is PanelPart p && p.PanelId == hitId)
                {
                    PartList.SelectedIndex = i;
                    break;
                }
            }
            if (_stage == "ops")
            {
                SetStatus($"选中 {hitId}");
                return;
            }
            if (_locked.Contains(hitId))
            {
                SetStatus($"已锁定 · {hitId}");
                return;
            }
            var (mx, my) = ScreenToSheet(x, y);
            _dragMode = "nest";
            _nestDragPanelId = hitId;
            _nestStartMx = mx;
            _nestStartMy = my;
            _nestOrigOx = place.OffsetX;
            _nestOrigOy = place.OffsetY;
            CanvasPane.CaptureMouse();
            SetStatus($"拖摆位 {hitId}");
            e.Handled = true;
        }
    }

    void OnCanvasMove(object sender, MouseEventArgs e)
    {
        var (x, y) = CanvasPixelPos(e);

        // hover cursor when not dragging
        if (_dragMode is null)
        {
            UpdateHoverCursor(x, y);
            if (e.LeftButton != MouseButtonState.Pressed) return;
        }

        if (_dragMode is null || e.LeftButton != MouseButtonState.Pressed) return;

        if (_dragMode == "geom" && _geomHit is not null && _geomStart is not null && _geomView is not null)
        {
            var (lx, ly) = GeomInteraction.ToLocal(_geomView.Value, x, y);
            _selected = GeomInteraction.ApplyDrag(_geomStart, _geomHit.Value, lx, ly);
            CanvasHost.InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_dragMode == "nest" && _nestDragPanelId is not null && _nest is { Ok: true })
        {
            var (mx, my) = ScreenToSheet(x, y);
            var ox = NestDrag.SnapMm(_nestOrigOx + (mx - _nestStartMx), 1);
            var oy = NestDrag.SnapMm(_nestOrigOy + (my - _nestStartMy), 1);
            var place = _nest.Placements.FirstOrDefault(p => p.PanelId == _nestDragPanelId);
            if (place is null || _session.Package is null) return;
            if (!_session.Package.Panels.ToDictionary(p => p.PanelId).TryGetValue(_nestDragPanelId, out var panel))
                return;
            var sw = ParseMm(StockWidthBox.Text, 1220);
            var sh = ParseMm(StockLengthBox.Text, 2440);
            var (cx, cy) = NestDrag.ClampOnSheet(panel, ox, oy, place.RotationDeg, sw, sh, ParseMm(NestBorderBox.Text, 15));
            place.OffsetX = cx;
            place.OffsetY = cy;
            CanvasHost.InvalidateVisual();
            e.Handled = true;
        }
    }

    void UpdateHoverCursor(float x, float y)
    {
        if (_stage == "load" && _selected is not null)
        {
            var w = _surfaceW > 0 ? _surfaceW : Math.Max(1, (int)(CanvasHost.ActualWidth * _dpiX));
            var h = _surfaceH > 0 ? _surfaceH : Math.Max(1, (int)(CanvasHost.ActualHeight * _dpiY));
            var view = GeomInteraction.BuildView(_selected, w, h);
            var hit = GeomInteraction.HitTest(_selected, view, x, y);
            if (hit is { Type: not "panel" })
            {
                CanvasPane.Cursor = Cursors.SizeAll;
                var hint = hit.Value.Type switch
                {
                    "hole" => $"瀛?{hit.Value.FeatureId}",
                    "groovePoint" => $"妲界 {hit.Value.FeatureId}",
                    "resize" => $"杈?{hit.Value.Edge}",
                    _ => hit.Value.Type,
                };
                if (_hoverHint != hint) { _hoverHint = hint; CanvasHost.InvalidateVisual(); }
                return;
            }
        }
        else if ((_stage is "nest") && _nest is { Ok: true })
        {
            EnsureNestViewMetrics();
            var id = HitTestNest(x, y);
            if (id is not null)
            {
                CanvasPane.Cursor = _locked.Contains(id) ? Cursors.No : Cursors.SizeAll;
                return;
            }
        }
        CanvasPane.Cursor = Cursors.Arrow;
        if (_hoverHint is not null) { _hoverHint = null; CanvasHost.InvalidateVisual(); }
    }

    void OnCanvasUp(object sender, MouseButtonEventArgs e) => EndCanvasDrag();

    void OnCanvasLostCapture(object sender, MouseEventArgs e) => EndCanvasDrag();

    void EndCanvasDrag()
    {
        if (_dragMode is null) return;

        if (_dragMode == "geom" && _selected is not null && _geomStart is not null)
        {
            if (!ReferenceEquals(_selected, _geomStart))
            {
                var draft = _selected;
                _selected = _geomStart;
                CommitPanel(draft);
            SetStatus("updated");
            }
        }
        else if (_dragMode == "nest" && _nestDragPanelId is not null && _nest is { Ok: true } && _session.Package is not null)
        {
            var place = _nest.Placements.FirstOrDefault(p => p.PanelId == _nestDragPanelId);
            if (place is not null)
            {
                var byId = _session.Package.Panels.ToDictionary(p => p.PanelId);
                if (byId.TryGetValue(_nestDragPanelId, out var panel))
                {
                    var others = _nest.Placements
                        .Where(p => p.PanelId != _nestDragPanelId)
                        .Select(p => (p.PanelId, p.SheetIndex, p.OffsetX, p.OffsetY, p.RotationDeg))
                        .ToList();
                    var (ox, oy, blocked) = NestDrag.Resolve(
                        panel, _nestDragPanelId, place.OffsetX, place.OffsetY, place.RotationDeg, place.SheetIndex,
                        others, byId,
                        ParseMm(StockWidthBox.Text, 1220), ParseMm(StockLengthBox.Text, 2440),
                        ParseMm(NestSpacingBox.Text, 12), ParseMm(NestBorderBox.Text, 15),
                        (_nestOrigOx, _nestOrigOy),
                        AllowOverlapChk.IsChecked == true);
                    place.OffsetX = ox;
                    place.OffsetY = oy;
            SetStatus("updated");
                    RefreshNestReport();
                    CanvasHost.InvalidateVisual();
                }
            }
        }

        _dragMode = null;
        _geomHit = null;
        _geomStart = null;
        _nestDragPanelId = null;
        if (CanvasPane.IsMouseCaptured) CanvasPane.ReleaseMouseCapture();
    }

    void EnsureNestViewMetrics()
    {
        if (_nestScale > 0 && _surfaceW > 0) return;
        var w = _surfaceW > 0 ? _surfaceW : Math.Max(1, (int)(CanvasHost.ActualWidth * _dpiX));
        var h = _surfaceH > 0 ? _surfaceH : Math.Max(1, (int)(CanvasHost.ActualHeight * _dpiY));
        var sw = (float)ParseMm(StockWidthBox.Text, 1220);
        var sh = (float)ParseMm(StockLengthBox.Text, 2440);
        var pad = 24f;
        var scale = Math.Min((w - 2 * pad) / sw, (h - 2 * pad) / sh);
        if (scale <= 0) return;
        _nestPad = pad;
        _nestScale = scale;
        _nestSheetW = sw;
        _nestSheetH = sh;
    }

    string? HitTestNest(float sx, float sy)
    {
        if (_nest is not { Ok: true } || _session.Package is null) return null;
        EnsureNestViewMetrics();
        var byId = _session.Package.Panels.ToDictionary(p => p.PanelId);
        var (lx, ly) = ScreenToSheet(sx, sy);
        foreach (var place in _nest.Placements.Where(p => p.SheetIndex == 0).Reverse())
        {
            if (!byId.TryGetValue(place.PanelId, out var panel)) continue;
            // AABB 鍏堟祴锛堝宸ぇ锛夛紝鍐嶈疆寤?
            var box = NestDrag.Aabb(panel, place.OffsetX, place.OffsetY, place.RotationDeg);
            const double pad = 2;
            if (lx < box.MinX - pad || lx > box.MaxX + pad || ly < box.MinY - pad || ly > box.MaxY + pad)
                continue;
            return place.PanelId; // AABB 鍛戒腑鍗冲彲鎷栵紙杞粨鍛戒腑鍦ㄦ棆杞笅鏄撴紡锛?
        }
        return null;
    }

    (double Mx, double My) ScreenToSheet(float sx, float sy)
    {
        if (_nestScale <= 0) return (0, 0);
        var mx = (sx - _nestPad) / _nestScale;
        var my = _nestSheetH - (sy - _nestPad) / _nestScale;
        return (mx, my);
    }

    static bool PointInOutline(double x, double y, PanelPart panel)
    {
        var pts = panel.Outline.Points;
        var inside = false;
        for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
        {
            var xi = pts[i].X;
            var yi = pts[i].Y;
            var xj = pts[j].X;
            var yj = pts[j].Y;
            var hit = yi > y != yj > y && x < (xj - xi) * (y - yi) / (yj - yi + 1e-12) + xi;
            if (hit) inside = !inside;
        }
        return inside;
    }

    void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        RefreshDpi();
        _surfaceW = e.Info.Width;
        _surfaceH = e.Info.Height;
        var canvas = e.Surface.Canvas;
        if (_showNest && _stage is "nest" or "ops")
        {
            var sheet = _session.Package?.Sheets.FirstOrDefault();
            var sw = (float)ParseMm(StockWidthBox.Text, sheet?.WidthMm > 0 ? sheet.WidthMm : 1220);
            var sh = (float)ParseMm(StockLengthBox.Text, sheet?.LengthMm > 0 ? sheet.LengthMm : 2440);
            var pad = 28f;
            var scale = Math.Min((e.Info.Width - 2 * pad) / sw, (e.Info.Height - 2 * pad) / sh);
            if (scale <= 0) return;
            _nestPad = pad;
            _nestScale = scale;
            _nestSheetW = sw;
            _nestSheetH = sh;

            var placements = _nest is { Ok: true } ? _nest.Placements.ToList() : [];
            var panels = _session.Package?.Panels.ToList() ?? [];
            CanvasPainter.PaintNest(canvas, e.Info.Width, e.Info.Height, panels, placements,
                new CanvasPainter.NestPaintOpts(
                    sw, sh, pad, scale,
                    _selected?.PanelId,
                    _locked,
                    CurrentConflicts(),
                    _opsOverlay,
                    ShowOps: _stage == "ops",
                    ActiveCamFrame: _stage == "ops" && _camFrames.Count > 0
                        ? _camFrames[_camFrameIndex]
                        : null));
            return;
        }

        CanvasPainter.PaintGeom(canvas, e.Info.Width, e.Info.Height, _selected, _hoverHint);
    }

    void SetStatus(string text) => StatusText.Text = text;
}
