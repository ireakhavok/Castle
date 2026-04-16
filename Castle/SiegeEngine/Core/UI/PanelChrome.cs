// Folder: SiegeEngine/Core/UI
// File: PanelChrome.cs
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using SiegeEngine.Core.Rendering;
using System.Numerics;

namespace SiegeEngine.Core.UI
{
    public class PanelChrome
    {
        private readonly BasePanel _owner;
        private readonly float _titleHeight = BasePanel.TitleHeight;
        public Vector4 close_color = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);//new Vector4(0.486f, 1.0f, 0.796f, 1.0f);

        public PanelChrome(BasePanel owner)
        {
            _owner = owner;
        }

        public bool HandleUpdate(Vector2 absMousePos, bool mousePressed, bool mouseReleased)
        {
            if (PanelManager.Current?.GetTopmostPanelAt(absMousePos) != _owner)
                return false;

            float closeX = _owner.Position.X + _owner.Size.X - 24f;
            bool overClose = absMousePos.X >= closeX && absMousePos.X <= _owner.Position.X + _owner.Size.X &&
                             absMousePos.Y >= _owner.Position.Y && absMousePos.Y <= _owner.Position.Y + _titleHeight;

            if (mouseReleased && overClose && _owner.IsClosable)
            {
                _owner.Close();
                return true;
            }

            bool overTitle = absMousePos.Y >= _owner.Position.Y && absMousePos.Y <= _owner.Position.Y + _titleHeight;
            if (_owner.AllowDragging && _owner.DockState == DockState.Floating && mousePressed && overTitle)
            {
                _owner.StartTitleBarDrag(absMousePos);
                return true;
            }
            return false;
        }

        public void Render(UIQuadRenderer quadRenderer, float panelWidth, float panelHeight)
        {
            quadRenderer.DrawQuad(0, 0, panelWidth, _titleHeight, new Vector4(0.2f, 0.2f, 0.2f, 1.0f), panelWidth, panelHeight);

            if (_owner.IsClosable)
            {
                float btnX = panelWidth - 26f;
                float btnY = 3f;
                float btnW = 20f;
                float btnH = _titleHeight - 6f;
                quadRenderer.DrawQuad(btnX, btnY, btnW, btnH, new Vector4(0.2f, 0.2f, 0.2f, 1.0f), panelWidth, panelHeight);

                float closeX = panelWidth - 24f;
                float closeY = (_titleHeight - 14f) * 0.5f;
                float len = 14f;
                float thick = 2.5f;

                quadRenderer.DrawLine(closeX, closeY, closeX + len, closeY + len, thick, close_color, panelWidth, panelHeight);
                quadRenderer.DrawLine(closeX + len, closeY, closeX, closeY + len, thick, close_color, panelWidth, panelHeight);
            }
        }

        public void Dispose() { }
    }
}