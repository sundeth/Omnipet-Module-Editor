using System;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

namespace OmnipetModuleEditor.Utils
{
    /// <summary>
    /// Implemented by editor tabs that support Ctrl+C / Ctrl+V duplication of
    /// the currently selected list or grid entry.
    /// </summary>
    public interface IListClipboardTarget
    {
        /// <summary>Copy the currently selected entry into the tab's paste buffer.</summary>
        void CopySelection();

        /// <summary>Paste a duplicate of the buffered entry into the list/grid.</summary>
        void PasteClipboard();
    }

    /// <summary>
    /// Helpers shared by the list/grid copy-paste support across editor tabs.
    /// </summary>
    public static class ClipboardListUtils
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        /// <summary>
        /// True when the control with keyboard focus is a text-entry control
        /// (TextBox, ComboBox, NumericUpDown, or a grid cell editor). In that
        /// case Ctrl+C / Ctrl+V must keep their normal text behaviour rather
        /// than duplicating a list entry.
        /// </summary>
        public static bool IsTextEntryFocused()
        {
            var focused = Control.FromHandle(GetFocus());
            if (focused == null) return false;
            if (focused is TextBoxBase) return true;      // includes grid text-cell editors
            if (focused is ComboBox) return true;         // includes grid combo-cell editors
            if (focused is NumericUpDown) return true;
            return false;
        }

        /// <summary>
        /// Deep-clones a JSON-serialisable model object so a pasted entry is an
        /// independent copy of its source. Returns default when source is null.
        /// </summary>
        public static T DeepClone<T>(T source)
        {
            if (source == null) return default(T);
            string json = JsonSerializer.Serialize(source);
            return JsonSerializer.Deserialize<T>(json);
        }
    }
}
