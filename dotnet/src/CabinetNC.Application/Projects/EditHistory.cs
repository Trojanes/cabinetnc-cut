namespace CabinetNC.Application.Projects;

using CabinetNC.Domain;

/// <summary>In-memory undo/redo snapshots of the runtime CutPackage JSON.</summary>
public sealed class EditHistory
{
    readonly Stack<string> _undo = new();
    readonly Stack<string> _redo = new();
    const int MaxDepth = 64;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public int UndoDepth => _undo.Count;

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    /// <summary>Push current package JSON before a mutating edit.</summary>
    public void PushBeforeEdit(string? packageJson)
    {
        if (string.IsNullOrEmpty(packageJson)) return;
        _undo.Push(packageJson);
        _redo.Clear();
        // Soft cap: drop oldest by rebuilding (rare).
        if (_undo.Count <= MaxDepth) return;
        var keep = _undo.Take(MaxDepth).Reverse().ToArray();
        _undo.Clear();
        foreach (var snap in keep)
            _undo.Push(snap);
    }

    public string? Undo(string currentJson)
    {
        if (_undo.Count == 0) return null;
        _redo.Push(currentJson);
        return _undo.Pop();
    }

    public string? Redo(string currentJson)
    {
        if (_redo.Count == 0) return null;
        _undo.Push(currentJson);
        return _redo.Pop();
    }
}
