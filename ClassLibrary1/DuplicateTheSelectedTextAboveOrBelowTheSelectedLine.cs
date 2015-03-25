using System.Windows.Forms;
using PluginCore;
using PluginCore.Managers;
using ScintillaNet;
using ScintillaNet.Lexers;

/// <summary>
/// Ctrl + Shift + UP or DOWN
/// Duplicate the selected text above or below the selected line
/// </summary>
public class DuplicateTheSelectedTextAboveOrBelowTheSelectedLine : IEventHandler
{
    private const string DUPLICATE_SELECTION_ABOVE_SELECTED_LINE = "EditMenu.DuplicateSelectionAboveSelectedLine";
    private const string DUPLICATE_SELECTION_BELOW_SELECTED_LINE = "DuplicateSelectionBelowSelectedLine";

    public static void Execute()
    {
        new DuplicateTheSelectedTextAboveOrBelowTheSelectedLine();
    }

    public DuplicateTheSelectedTextAboveOrBelowTheSelectedLine()
    {
        PluginBase.MainForm.RegisterShortcutItem(DUPLICATE_SELECTION_ABOVE_SELECTED_LINE, Keys.Control | Keys.Shift | Keys.Up);
        PluginBase.MainForm.RegisterShortcutItem(DUPLICATE_SELECTION_BELOW_SELECTED_LINE, Keys.Control | Keys.Shift | Keys.Down);
        EventManager.AddEventHandler(this, EventType.Keys);
    }

    /// <summary>
    /// Handles the incoming events
    /// </summary>
    public void HandleEvent(object sender, NotifyEvent e, HandlingPriority priority)
    {
        ScintillaControl sci = PluginBase.MainForm.CurrentDocument.SciControl;
        if (sci == null) return;
        switch (e.Type)
        {
            case EventType.Keys:
                KeyEvent ke = (KeyEvent) e;
                if (ke.Value == PluginBase.MainForm.GetShortcutItemKeys(DUPLICATE_SELECTION_ABOVE_SELECTED_LINE))
                {
                    string text = sci.SelText;
                    int currentPos = sci.CurrentPos;
                    if (string.IsNullOrEmpty(text))
                    {
                        int prevSciLength = sci.Length;
                        sci.LineDuplicate();
                        currentPos = currentPos + (sci.Length - prevSciLength);
                        sci.SetSel(currentPos, currentPos);
                    }
                    else
                    {
                        int line = sci.CurrentLine;
                        if (line == 0) sci.SetSel(0, 0);
                        else
                        {
                            sci.GotoLine(line - 1);
                            sci.LineEnd();
                        }
                        sci.NewLine();
                        currentPos = sci.CurrentPos;
                        sci.InsertText(currentPos, text);
                        sci.SetSel(currentPos, currentPos);
                    }
                }
                else if (ke.Value == PluginBase.MainForm.GetShortcutItemKeys(DUPLICATE_SELECTION_BELOW_SELECTED_LINE))
                {
                    string text = sci.SelText;
                    int currentPos = sci.CurrentPos;
                    if (string.IsNullOrEmpty(text))
                    {
                        sci.LineDuplicate();
                        sci.MoveLineDown();
                    }
                    else
                    {
                        sci.LineEnd();
                        sci.NewLine();
                        currentPos = sci.CurrentPos;
                        sci.InsertText(currentPos, text);
                    }
                    sci.SetSel(currentPos, currentPos);
                }
                break;
        }
    }
}