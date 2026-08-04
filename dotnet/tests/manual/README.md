# Desktop smoke / manual QA

## 自动冒烟

```powershell
cd d:\project\cabinetnc-cut\dotnet
python -m pip install -r tests\manual\requirements.txt
python tests\manual\smoke_desktop.py `
  --json artifacts\smoke-latest.json `
  --keep-open
```

- 脚本会关闭旧 Desktop/Worker，启动最新 Debug Desktop。
- 覆盖启动门禁、七模块、120 板导入、密排/NC、Clipper 校验、刀补、CAM、刀具应用、预检/导出入口。
- 不带 `--keep-open` 时测试后关闭应用。
- 退出码 `0` 表示全部通过，结果写入 JSON。

## 手工案例

完整案例与结果记录模板：

`docs/testing/SMOKE_CASE_LIBRARY.md`

非法输入 fixture：

`tests/manual/fixtures/invalid_empty.cut.json`
