// Folder: Keystone
// File: IEditorCommand.cs
namespace Keystone
{
    public interface IEditorCommand
    {
        string Description { get; }
        void Execute();
        void Undo();
        bool TryMerge(IEditorCommand incoming);
    }
}
