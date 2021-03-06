using System;
using System.Reflection;
using System.Collections;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ScintillaNet.Configuration;
using System.Drawing.Printing;
using PluginCore.FRService;
using PluginCore.Utilities;
using PluginCore.Managers;
using System.Drawing;
using System.Text;
using PluginCore;

namespace ScintillaNet
{
	public class ScintillaControl : Control
	{
        private bool saveBOM;
        private Encoding encoding;
		private int directPointer;
		private IntPtr hwndScintilla;
        private bool hasHighlights = false;
		private bool ignoreAllKeys = false;
		private bool isBraceMatching = true;
        private bool isHiliteSelected = true;
        private bool useHighlightGuides = true;
		private static Scintilla sciConfiguration = null;
        private static Hashtable shortcutOverrides = new Hashtable();
        private Enums.IndentView indentView = Enums.IndentView.Real;
		private Enums.SmartIndent smartIndent = Enums.SmartIndent.CPP;
		private Hashtable ignoredKeys = new Hashtable();
        private string configLanguage = String.Empty;
        private string fileName = String.Empty;
		
		#region Scintilla Main

        public ScintillaControl() : this("SciLexer.dll")
        {
            DragAcceptFiles(this.Handle, 1);
        }

        public ScintillaControl(string fullpath)
        {
            try
            {
                IntPtr lib = WinAPI.LoadLibrary(fullpath);
                hwndScintilla = WinAPI.CreateWindowEx(0, "Scintilla", "", WS_CHILD_VISIBLE_TABSTOP, 0, 0, this.Width, this.Height, this.Handle, 0, new IntPtr(0), null);
                directPointer = (int)SlowPerform(2185, 0, 0);
                UpdateUI += new UpdateUIHandler(OnBraceMatch);
                UpdateUI += new UpdateUIHandler(OnCancelHighlight);
                DoubleClick += new DoubleClickHandler(OnBlockSelect);
                DoubleClick += new DoubleClickHandler(OnSelectHighlight);
                CharAdded += new CharAddedHandler(OnSmartIndent);
                Resize += new EventHandler(OnResize);
                directPointer = DirectPointer;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public void OnResize(object sender, EventArgs e)
        {
            SetWindowPos(this.hwndScintilla, 0, this.ClientRectangle.X, this.ClientRectangle.Y, this.ClientRectangle.Width, this.ClientRectangle.Height, 0);
        }

		#endregion

		#region Scintilla Event Members
		
		public event KeyHandler Key;
		public event ZoomHandler Zoom;
        public event FocusHandler FocusChanged;
		public event StyleNeededHandler StyleNeeded;
        public event CharAddedHandler CharAdded;
        public event SavePointReachedHandler SavePointReached;
        public event SavePointLeftHandler SavePointLeft;
        public event ModifyAttemptROHandler ModifyAttemptRO;
        public event UpdateUIHandler UpdateUI;
        public event ModifiedHandler Modified;
        public event MacroRecordHandler MacroRecord;
        public event MarginClickHandler MarginClick;
        public event NeedShownHandler NeedShown;
        public event PaintedHandler Painted;
        public event UserListSelectionHandler UserListSelection;
        public event URIDroppedHandler URIDropped;
        public event DwellStartHandler DwellStart;
        public event DwellEndHandler DwellEnd;
        public event HotSpotClickHandler HotSpotClick;
        public event HotSpotDoubleClickHandler HotSpotDoubleClick;
        public event CallTipClickHandler CallTipClick;
        public event AutoCSelectionHandler AutoCSelection;
		public event TextInsertedHandler TextInserted;
		public event TextDeletedHandler TextDeleted;
		public event FoldChangedHandler FoldChanged;
		public event UserPerformedHandler UserPerformed;
		public event UndoPerformedHandler UndoPerformed;
		public event RedoPerformedHandler RedoPerformed;
		public event LastStepInUndoRedoHandler LastStepInUndoRedo;
		public event MarkerChangedHandler MarkerChanged;
		public event BeforeInsertHandler BeforeInsert;
		public event BeforeDeleteHandler BeforeDelete;
		public event SmartIndentHandler SmartIndent;
		public new event StyleChangedHandler StyleChanged;
		public new event DoubleClickHandler DoubleClick;
        public event IndicatorClickHandler IndicatorClick;
        public event IndicatorReleaseHandler IndicatorRelease;
        public event AutoCCancelledHandler AutoCCancelled;
        public event AutoCCharDeletedHandler AutoCCharDeleted;
        public event UpdateSyncHandler UpdateSync;
		
		#endregion

		#region Scintilla Properties

        /// <summary>
        /// Gets the sci handle
        /// </summary>
        public IntPtr HandleSci
        {
            get { return hwndScintilla; }
        }

        /// <summary>
        /// Current used configuration
        /// </summary> 
        static public Scintilla Configuration
		{
			get 
            { 
                return sciConfiguration; 
            }
			set 
            { 
                sciConfiguration = value; 
            }
		}

        /// <summary>
        /// Indent view type
        /// </summary>
        public Enums.IndentView IndentView
        {
            get
            {
                return this.indentView;
            }
            set
            {
                this.indentView = value;
            }
        }

        /// <summary>
        /// Current configuration language
        /// </summary>
        public string ConfigurationLanguage
        {
            get 
            { 
                return this.configLanguage;
            }
            set
            {
                if (value == null || value.Equals("")) return;
                this.SetLanguage(value);
                
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void SetLanguage(String value)
        {
            Language lang = sciConfiguration.GetLanguage(value);
            if (lang == null) return;
            StyleClearAll();
            try
            {
                lang.lexer.key = (int)Enum.Parse(typeof(Enums.Lexer), lang.lexer.name, true);
            }
            catch { /* If not found, uses the lang.lexer.key directly. */ }
            this.configLanguage = value;
            Lexer = lang.lexer.key;
            if (lang.lexer.stylebits > 0) StyleBits = lang.lexer.stylebits;
            if (lang.editorstyle != null)
            {
                EdgeColour = lang.editorstyle.PrintMarginColor;
                CaretFore = lang.editorstyle.CaretForegroundColor;
                CaretLineBack = lang.editorstyle.CaretLineBackgroundColor;
                SetSelBack(true, lang.editorstyle.SelectionBackgroundColor);
                SetSelFore(true, lang.editorstyle.SelectionForegroundColor);
                SetFoldMarginHiColour(true, lang.editorstyle.MarginForegroundColor);
                SetFoldMarginColour(true, lang.editorstyle.MarginBackgroundColor);
                Int32 markerForegroundColor = lang.editorstyle.MarkerForegroundColor;
                Int32 markerBackgroundColor = lang.editorstyle.MarkerBackgroundColor;
                MarkerSetBack((Int32)ScintillaNet.Enums.MarkerOutline.Folder, markerBackgroundColor);
                MarkerSetFore((Int32)ScintillaNet.Enums.MarkerOutline.Folder, markerForegroundColor);
                MarkerSetBack((Int32)ScintillaNet.Enums.MarkerOutline.FolderOpen, markerBackgroundColor);
                MarkerSetFore((Int32)ScintillaNet.Enums.MarkerOutline.FolderOpen, markerForegroundColor);
                MarkerSetBack((Int32)ScintillaNet.Enums.MarkerOutline.FolderSub, markerBackgroundColor);
                MarkerSetFore((Int32)ScintillaNet.Enums.MarkerOutline.FolderSub, markerForegroundColor);
                MarkerSetBack((Int32)ScintillaNet.Enums.MarkerOutline.FolderTail, markerBackgroundColor);
                MarkerSetFore((Int32)ScintillaNet.Enums.MarkerOutline.FolderTail, markerForegroundColor);
                MarkerSetBack((Int32)ScintillaNet.Enums.MarkerOutline.FolderEnd, markerBackgroundColor);
                MarkerSetFore((Int32)ScintillaNet.Enums.MarkerOutline.FolderEnd, markerForegroundColor);
                MarkerSetBack((Int32)ScintillaNet.Enums.MarkerOutline.FolderOpenMid, markerBackgroundColor);
                MarkerSetFore((Int32)ScintillaNet.Enums.MarkerOutline.FolderOpenMid, markerForegroundColor);
                MarkerSetBack((Int32)ScintillaNet.Enums.MarkerOutline.FolderMidTail, markerBackgroundColor);
                MarkerSetFore((Int32)ScintillaNet.Enums.MarkerOutline.FolderMidTail, markerForegroundColor);
                MarkerSetBack(0, lang.editorstyle.BookmarkLineColor);
                MarkerSetBack(2, lang.editorstyle.ModifiedLineColor);
            }
            if (lang.characterclass != null)
            {
                WordChars(lang.characterclass.Characters);
            }
            for (int j = 0; j < lang.usestyles.Length; j++)
            {
                UseStyle usestyle = lang.usestyles[j];
                if (usestyle.key == 0)
                {
                    System.Type theType = null;
                    switch ((Enums.Lexer)lang.lexer.key)
                    {
                        case Enums.Lexer.PYTHON:
                            theType = typeof(Lexers.PYTHON);
                            break;
                        case Enums.Lexer.CPP:
                            theType = typeof(Lexers.CPP);
                            break;
                        case Enums.Lexer.HTML:
                            theType = typeof(Lexers.HTML);
                            break;
                        case Enums.Lexer.XML:
                            theType = typeof(Lexers.XML);
                            break;
                        case Enums.Lexer.PERL:
                            theType = typeof(Lexers.PERL);
                            break;
                        case Enums.Lexer.SQL:
                            theType = typeof(Lexers.SQL);
                            break;
                        case Enums.Lexer.VB:
                            theType = typeof(Lexers.VB);
                            break;
                        case Enums.Lexer.PROPERTIES:
                            theType = typeof(Lexers.PROPERTIES);
                            break;
                        case Enums.Lexer.ERRORLIST:
                            theType = typeof(Lexers.ERRORLIST);
                            break;
                        case Enums.Lexer.MAKEFILE:
                            theType = typeof(Lexers.MAKEFILE);
                            break;
                        case Enums.Lexer.BATCH:
                            theType = typeof(Lexers.BATCH);
                            break;
                        case Enums.Lexer.LATEX:
                            theType = typeof(Lexers.LATEX);
                            break;
                        case Enums.Lexer.LUA:
                            theType = typeof(Lexers.LUA);
                            break;
                        case Enums.Lexer.DIFF:
                            theType = typeof(Lexers.DIFF);
                            break;
                        case Enums.Lexer.CONF:
                            theType = typeof(Lexers.CONF);
                            break;
                        case Enums.Lexer.PASCAL:
                            theType = typeof(Lexers.PASCAL);
                            break;
                        case Enums.Lexer.AVE:
                            theType = typeof(Lexers.AVE);
                            break;
                        case Enums.Lexer.ADA:
                            theType = typeof(Lexers.ADA);
                            break;
                        case Enums.Lexer.LISP:
                            theType = typeof(Lexers.LISP);
                            break;
                        case Enums.Lexer.RUBY:
                            theType = typeof(Lexers.RUBY);
                            break;
                        case Enums.Lexer.EIFFEL:
                            theType = typeof(Lexers.EIFFEL);
                            break;
                        case Enums.Lexer.EIFFELKW:
                            theType = typeof(Lexers.EIFFELKW);
                            break;
                        case Enums.Lexer.TCL:
                            theType = typeof(Lexers.TCL);
                            break;
                        case Enums.Lexer.NNCRONTAB:
                            theType = typeof(Lexers.NNCRONTAB);
                            break;
                        case Enums.Lexer.BULLANT:
                            theType = typeof(Lexers.BULLANT);
                            break;
                        case Enums.Lexer.VBSCRIPT:
                            theType = typeof(Lexers.VBSCRIPT);
                            break;
                        case Enums.Lexer.BAAN:
                            theType = typeof(Lexers.BAAN);
                            break;
                        case Enums.Lexer.MATLAB:
                            theType = typeof(Lexers.MATLAB);
                            break;
                        case Enums.Lexer.SCRIPTOL:
                            theType = typeof(Lexers.SCRIPTOL);
                            break;
                        case Enums.Lexer.ASM:
                            theType = typeof(Lexers.ASM);
                            break;
                        case Enums.Lexer.FORTRAN:
                            theType = typeof(Lexers.FORTRAN);
                            break;
                        case Enums.Lexer.F77:
                            theType = typeof(Lexers.F77);
                            break;
                        case Enums.Lexer.CSS:
                            theType = typeof(Lexers.CSS);
                            break;
                        case Enums.Lexer.POV:
                            theType = typeof(Lexers.POV);
                            break;
                        case Enums.Lexer.LOUT:
                            theType = typeof(Lexers.LOUT);
                            break;
                        case Enums.Lexer.ESCRIPT:
                            theType = typeof(Lexers.ESCRIPT);
                            break;
                        case Enums.Lexer.PS:
                            theType = typeof(Lexers.PS);
                            break;
                        case Enums.Lexer.NSIS:
                            theType = typeof(Lexers.NSIS);
                            break;
                        case Enums.Lexer.MMIXAL:
                            theType = typeof(Lexers.MMIXAL);
                            break;
                        case Enums.Lexer.LOT:
                            theType = typeof(Lexers.LOT);
                            break;
                        case Enums.Lexer.YAML:
                            theType = typeof(Lexers.YAML);
                            break;
                        case Enums.Lexer.TEX:
                            theType = typeof(Lexers.TEX);
                            break;
                        case Enums.Lexer.METAPOST:
                            theType = typeof(Lexers.METAPOST);
                            break;
                        case Enums.Lexer.POWERBASIC:
                            theType = typeof(Lexers.POWERBASIC);
                            break;
                        case Enums.Lexer.FORTH:
                            theType = typeof(Lexers.FORTH);
                            break;
                        case Enums.Lexer.ERLANG:
                            theType = typeof(Lexers.ERLANG);
                            break;
                        case Enums.Lexer.OCTAVE:
                            theType = typeof(Lexers.OCTAVE);
                            break;
                        case Enums.Lexer.MSSQL:
                            theType = typeof(Lexers.MSSQL);
                            break;
                        case Enums.Lexer.VERILOG:
                            theType = typeof(Lexers.VERILOG);
                            break;
                        case Enums.Lexer.KIX:
                            theType = typeof(Lexers.KIX);
                            break;
                        case Enums.Lexer.GUI4CLI:
                            theType = typeof(Lexers.GUI4CLI);
                            break;
                        case Enums.Lexer.SPECMAN:
                            theType = typeof(Lexers.SPECMAN);
                            break;
                        case Enums.Lexer.AU3:
                            theType = typeof(Lexers.AU3);
                            break;
                        case Enums.Lexer.APDL:
                            theType = typeof(Lexers.APDL);
                            break;
                        case Enums.Lexer.BASH:
                            theType = typeof(Lexers.BASH);
                            break;
                        case Enums.Lexer.ASN1:
                            theType = typeof(Lexers.ASN1);
                            break;
                        case Enums.Lexer.VHDL:
                            theType = typeof(Lexers.VHDL);
                            break;
                        case Enums.Lexer.CAML:
                            theType = typeof(Lexers.CAML);
                            break;
                        case Enums.Lexer.HASKELL:
                            theType = typeof(Lexers.HASKELL);
                            break;
                        case Enums.Lexer.TADS3:
                            theType = typeof(Lexers.TADS3);
                            break;
                        case Enums.Lexer.REBOL:
                            theType = typeof(Lexers.REBOL);
                            break;
                        case Enums.Lexer.SMALLTALK:
                            theType = typeof(Lexers.SMALLTALK);
                            break;
                        case Enums.Lexer.FLAGSHIP:
                            theType = typeof(Lexers.FLAGSHIP);
                            break;
                        case Enums.Lexer.CSOUND:
                            theType = typeof(Lexers.CSOUND);
                            break;
                        case Enums.Lexer.INNOSETUP:
                            theType = typeof(Lexers.INNOSETUP);
                            break;
                        case Enums.Lexer.OPAL:
                            theType = typeof(Lexers.OPAL);
                            break;
                        case Enums.Lexer.SPICE:
                            theType = typeof(Lexers.SPICE);
                            break;
                        case Enums.Lexer.D:
                            theType = typeof(Lexers.D);
                            break;
                        case Enums.Lexer.CMAKE:
                            theType = typeof(Lexers.CMAKE);
                            break;
                        case Enums.Lexer.GAP:
                            theType = typeof(Lexers.GAP);
                            break;
                        case Enums.Lexer.PLM:
                            theType = typeof(Lexers.PLM);
                            break;
                        case Enums.Lexer.PROGRESS:
                            theType = typeof(Lexers.PROGRESS);
                            break;
                        case Enums.Lexer.ABAQUS:
                            theType = typeof(Lexers.ABAQUS);
                            break;
                        case Enums.Lexer.ASYMPTOTE:
                            theType = typeof(Lexers.ASYMPTOTE);
                            break;
                        case Enums.Lexer.R:
                            theType = typeof(Lexers.R);
                            break;
                        case Enums.Lexer.MAGIK:
                            theType = typeof(Lexers.MAGIK);
                            break;
                        case Enums.Lexer.POWERSHELL:
                            theType = typeof(Lexers.POWERSHELL);
                            break;
                        case Enums.Lexer.MYSQL:
                            theType = typeof(Lexers.MYSQL);
                            break;
                        case Enums.Lexer.PO:
                            theType = typeof(Lexers.PO);
                            break;
                        case Enums.Lexer.SORCUS:
                            theType = typeof(Lexers.SORCUS);
                            break;
                        case Enums.Lexer.POWERPRO:
                            theType = typeof(Lexers.POWERPRO);
                            break;
                        case Enums.Lexer.NIMROD:
                            theType = typeof(Lexers.NIMROD);
                            break;
                        case Enums.Lexer.SML:
                            theType = typeof(Lexers.SML);
                            break;
                    }
                    try
                    {
                        usestyle.key = (int)Enum.Parse(theType, usestyle.name, true);
                    }
                    catch (Exception ex)
                    {
                        String info;
                        if (theType == null)
                        {
                            info = String.Format("Lexer '{0}' ({1}) unknown.", lang.lexer.name, lang.lexer.key);
                            ErrorManager.ShowWarning(info, ex);
                            break;
                        }
                        else
                        {
                            info = String.Format("Style '{0}' in syntax file is not used by lexer '{1}'.", usestyle.name, theType.Name);
                            ErrorManager.ShowWarning(info, ex);
                        }
                    }
                }
                // Set whitespace fore color to indentguide color
                if (usestyle.key == (Int32)ScintillaNet.Enums.StylesCommon.IndentGuide)
                {
                    SetWhitespaceFore(true, usestyle.ForegroundColor);
                }
                if (usestyle.HasForegroundColor) StyleSetFore(usestyle.key, usestyle.ForegroundColor);
                if (usestyle.HasBackgroundColor) StyleSetBack(usestyle.key, usestyle.BackgroundColor);
                if (usestyle.HasFontName) StyleSetFont(usestyle.key, usestyle.FontName);
                if (usestyle.HasFontSize) StyleSetSize(usestyle.key, usestyle.FontSize);
                if (usestyle.HasBold) StyleSetBold(usestyle.key, usestyle.IsBold);
                if (usestyle.HasItalics) StyleSetItalic(usestyle.key, usestyle.IsItalics);
                if (usestyle.HasEolFilled) StyleSetEOLFilled(usestyle.key, usestyle.IsEolFilled);
            }
            // Clear the keywords lists	
            for (int j = 0; j < 9; j++) KeyWords(j, "");
            for (int j = 0; j < lang.usekeywords.Length; j++)
            {
                UseKeyword usekeyword = lang.usekeywords[j];
                KeywordClass kc = sciConfiguration.GetKeywordClass(usekeyword.cls);
                if (kc != null) KeyWords(usekeyword.key, kc.val);
            }
            if (UpdateSync != null) this.UpdateSync(this);
        }

        /// <summary>
        /// Texts in the control
        /// </summary> 
        public override string Text
        {
            get 
            { 
                return GetText(Length); 
            }
            set 
            { 
                SetText(value); 
            }
        }

        /// <summary>
        /// Filename of the editable document
        /// </summary> 
        public string FileName
        {
            get 
            { 
                return fileName; 
            }
            set 
            { 
                fileName = value;
                if (UpdateSync != null) this.UpdateSync(this);
            }
        }

        /// <summary>
        /// Is the control focused?
        /// </summary> 
        public override bool Focused
        {
            get 
            { 
                return IsFocus; 
            }
        }

        /// <summary>
        /// Should we ignore recieved keys? 
        /// </summary> 
        public bool IgnoreAllKeys
        {
            get 
            { 
                return ignoreAllKeys; 
            }
            set 
            { 
                ignoreAllKeys = value;
            }
        }

        /// <summary>
        /// Enables the selected word highlighting.
        /// </summary>
        public bool IsHiliteSelected
        {
            get
            {
                return isHiliteSelected;
            }
            set
            {
                isHiliteSelected = value;
                if (UpdateSync != null) this.UpdateSync(this);
            }
        }

		/// <summary>
		/// Enables the brace matching from current position.
		/// </summary>
		public bool IsBraceMatching
		{
			get 
            { 
                return isBraceMatching; 
            }
			set 
            { 
                isBraceMatching = value;
                if (UpdateSync != null) this.UpdateSync(this);
            }
		}

        /// <summary>
        /// Enables the highlight guides from current position.
        /// </summary>
        public bool UseHighlightGuides
        {
            get
            {
                return useHighlightGuides;
            }
            set
            {
                useHighlightGuides = value;
                if (UpdateSync != null) this.UpdateSync(this);
            }
        }

		/// <summary>
		/// Enables the Smart Indenter so that On enter, it indents the next line.
		/// </summary>
		public Enums.SmartIndent SmartIndentType
		{
			get 
            { 
                return smartIndent; 
            }
			set 
            { 
                smartIndent = value;
                if (UpdateSync != null) this.UpdateSync(this);
            }
		}
		
		/// <summary>
		/// Are white space characters currently visible?
        /// Returns one of Enums.WhiteSpace constants.
		/// </summary>
		public Enums.WhiteSpace ViewWhitespace
		{
			get 
            { 
                return (Enums.WhiteSpace)ViewWS; 
            }
			set 
            { 
                ViewWS = (int)value; 
            }
		}

        /// <summary>
        /// Get or sets the background alpha of the caret line.
        /// </summary>
        public int CaretLineBackAlpha
        {
            get 
            { 
                return (int)SPerform(2471, 0, 0);
            }
            set
            {
                SPerform(2470, (uint)value, 0);
            }
        }

        /// <summary>
        /// Get or sets the background alpha of the caret line.
        /// </summary>
        public int CaretStyle
        {
            get
            {
                return (int)SPerform(2513, 0, 0);
            }
            set
            {
                SPerform(2512, (uint)value, 0);
            }
        }

        /// <summary>
        /// Sets or gets the indicator used for IndicatorFillRange and IndicatorClearRange
        /// </summary>
        public int CurrentIndicator
        {
            get
            {
                return (int)SPerform(2501, 0, 0);
            }
            set
            {
                SPerform(2500, (uint)value, 0);
            }
        }

        /// <summary>
        /// Gets or sets the number of entries in position cache.
        /// </summary>
        public int PositionCache
        {
            get
            {
                return (int)SPerform(2515, 0, 0);
            }
            set
            {
                SPerform(2514, (uint)value, 0);
            }
        }

        /// <summary>
        /// Get the current indicator value or sets the value used for IndicatorFillRange.
        /// </summary>
        public int IndicatorValue
        {
            get
            {
                return (int)SPerform(2503, 0, 0);
            }
            set
            {
                SPerform(2502, (uint)value, 0);
            }
        }

		/// <summary>
		/// Retrieve the current end of line mode - one of CRLF, CR, or LF.
		/// </summary>
		public Enums.EndOfLine EndOfLineMode
		{
			get 
            { 
                return (Enums.EndOfLine)EOLMode; 
            }
			set 
            { 
                EOLMode = (int)value;
            }
		}
		
		/// <summary>
		/// Length Method for : Retrieve the text of the line containing the caret.
		/// Returns the index of the caret on the line.
		/// </summary>
		public int CurLineSize
		{	
			get
			{
				return (int)SPerform(2027, 0 , 0);
			}
		}
		
		/// <summary>
		/// Length Method for : Retrieve the contents of a line.
		/// Returns the length of the line.
		/// </summary>
		public int LineSize
		{	
			get
			{
				return (int)SPerform(2153, 0, 0);
			}
		}
		
		/// <summary>
		/// Length Method for : Retrieve the selected text.
		/// Return the length of the text.
		/// </summary>
		public int SelTextSize
		{	
			get
			{
				return (int)SPerform(2161, 0, 0) - 1;
			}
		}
		
		/// <summary>
		/// Length Method for : Retrieve all the text in the document.
		/// Returns number of characters retrieved.
		/// </summary>
		public int TextSize
		{	
			get
			{
				return (int)SPerform(2182, 0, 0);
			}
		}
		
		/// <summary>
		/// Are there any redoable actions in the undo history?
		/// </summary>
		public bool CanRedo
		{
			get 
			{
				return SPerform(2016, 0, 0) != 0;
			}
		}
		
		/// <summary>
		/// Is there an auto-completion list visible?
		/// </summary>
		public bool IsAutoCActive
		{
			get 
			{
				return SPerform(2102, 0, 0) != 0;
			}
		}	

		/// <summary>
		/// Retrieve the position of the caret when the auto-completion list was displayed.
		/// </summary>
		public int AutoCPosStart
		{
			get 
			{
				return (int)SPerform(2103, 0, 0);
			}
		}	

		/// <summary>
		/// Will a paste succeed?
		/// </summary>
		public bool CanPaste
		{
			get 
			{
				return SPerform(2173, 0, 0) != 0;
			}
		}	

		/// <summary>
		/// Are there any undoable actions in the undo history?
		/// </summary>
		public bool CanUndo
		{
			get 
			{
				return SPerform(2174, 0, 0) != 0;
			}
		}	

		/// <summary>
		/// Is there an active call tip?
		/// </summary>
		public bool IsCallTipActive
		{
			get 
			{
				return SPerform(2202, 0, 0) != 0;
			}
		}	

		/// <summary>
		/// Retrieve the position where the caret was before displaying the call tip.
		/// </summary>
		public int CallTipPosStart
		{
			get 
			{
				return (int)SPerform(2203, 0, 0);
			}
		}	
		
		/// <summary>
		/// Create a new document object.
		/// Starts with reference count of 1 and not selected into editor.
		/// </summary>
		public int CreateDocument
		{
			get 
			{
				return (int)SPerform(2375, 0, 0);
			}
		}	

		/// <summary>
		/// Get currently selected item position in the auto-completion list
		/// </summary>
		public int AutoCGetCurrent
		{
			get 
			{
				return (int)SPerform(2445, 0, 0);
			}
		}	

		/// <summary>
		/// Returns the number of characters in the document.
		/// </summary>
		public int Length
		{
			get 
			{
				return (int)SPerform(2006, 0, 0); 
			}
		}
		
		/// <summary>
		/// Enable/Disable convert-on-paste for line endings
		/// </summary>
		public bool PasteConvertEndings
		{
			get 
			{
				return SPerform(2468, 0, 0) != 0; 
			}
			set 
			{
				SPerform(2467, (uint)(value ? 1 : 0), 0);
			}
		}
		
		/// <summary>
		/// Returns the position of the caret.
		/// </summary>
		public int CurrentPos
		{
			get 
			{
				return (int)SPerform(2008, 0, 0); 
			}
			set 
			{
				SPerform(2141, (uint)value , 0);
			}
		}

        /// <summary>
        /// Returns the chracter at the caret posiion.
        /// </summary>
        public char CurrentChar
        {
            get
            {
                return (char)CharAt(CurrentPos);
            }
        }
		
		/// <summary>
		/// Returns the position of the opposite end of the selection to the caret.
		/// </summary>
		public int AnchorPosition
		{
			get 
			{
				return (int)SPerform(2009, 0, 0);
			}
			set
			{
				SPerform(2026, (uint)value , 0);
			}
		}

		/// <summary>
		/// Is undo history being collected?
		/// </summary>
		public bool IsUndoCollection
		{
			get 
			{
				return SPerform(2019, 0, 0)!=0;
			}
			set
			{
				SPerform(2012 , (uint)(value ? 1 : 0), 0);
			}
		}	

		/// <summary>
		/// Are white space characters currently visible?
		/// Returns one of SCWS_/// constants.
		/// </summary>
		public int ViewWS
		{
			get 
			{
				return (int)SPerform(2020, 0, 0);
			}
			set
			{
				SPerform(2021, (uint)value , 0);
			}
		}	

		/// <summary>
		/// Retrieve the position of the last correctly styled character.
		/// </summary>
		public int EndStyled
		{
			get 
			{
				return (int)SPerform(2028, 0, 0);
			}
		}	

		/// <summary>
		/// Retrieve the current end of line mode - one of CRLF, CR, or LF.
		/// </summary>
		public int EOLMode
		{
			get 
			{
				return (int)SPerform(2030, 0, 0);
			}
			set
			{
				SPerform(2031, (uint)value , 0);
			}
		}	

		/// <summary>
		/// Is drawing done first into a buffer or direct to the screen?
		/// </summary>
		public bool IsBufferedDraw
		{
			get 
			{
				return SPerform(2034, 0, 0)!=0;
			}
			set
			{
				SPerform(2035 , (uint)(value ? 1 : 0), 0);
			}
		}	

		/// <summary>
		/// Retrieve the visible size of a tab.
		/// </summary>
		public int TabWidth
		{
			get 
			{
				return (int)SPerform(2121, 0, 0);
			}
			set
			{
				SPerform(2036, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Get the time in milliseconds that the caret is on and off.
		/// </summary>
		public int CaretPeriod
		{
			get 
			{
				return (int)SPerform(2075, 0, 0);
			}
			set
			{
				SPerform(2076, (uint)value, 0);
			}
		}
		
		/// <summary>
		/// Retrieve number of bits in style bytes used to hold the lexical state.
		/// </summary>
		public int StyleBits
		{
			get 
			{
				return (int)SPerform(2091, 0, 0);
			}
			set
			{
				SPerform(2090, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Retrieve the last line number that has line state.
		/// </summary>
		public int MaxLineState
		{
			get 
			{
				return (int)SPerform(2094, 0, 0);
			}
		}	

		/// <summary>
		/// Is the background of the line containing the caret in a different colour?
		/// </summary>
		public bool IsCaretLineVisible
		{
			get 
			{
				return SPerform(2095, 0, 0) != 0;
			}
			set
			{
				SPerform(2096, (uint)(value ? 1 : 0), 0);
			}
		}	

		/// <summary>
		/// Get the colour of the background of the line containing the caret.
		/// </summary>
		public int CaretLineBack
		{
			get 
			{
				return (int)SPerform(2097, 0, 0);
			}
			set
			{
				SPerform(2098, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Retrieve the auto-completion list separator character.
		/// </summary>
		public int AutoCSeparator
		{
			get 
			{
				return (int)SPerform(2107, 0, 0);
			}
			set
			{
				SPerform(2106, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Retrieve whether auto-completion cancelled by backspacing before start.
		/// </summary>
		public bool IsAutoCGetCancelAtStart
		{
			get 
			{
				return SPerform(2111, 0, 0) != 0;
			}
			set
			{
				SPerform(2110 , (uint)(value ? 1 : 0), 0);
			}
		}	

		/// <summary>
		/// Retrieve whether a single item auto-completion list automatically choose the item.
		/// </summary>
		public bool IsAutoCGetChooseSingle
		{
			get 
			{
				return SPerform(2114, 0, 0) != 0;
			}
			set
			{
				SPerform(2113, (uint)(value ? 1 : 0), 0);
			}
		}	

		/// <summary>
		/// Retrieve state of ignore case flag.
		/// </summary>
		public bool IsAutoCGetIgnoreCase
		{
			get 
			{
				return SPerform(2116, 0, 0) != 0;
			}
			set
			{
				SPerform(2115, (uint)(value ? 1 : 0), 0);
			}
		}	

		/// <summary>
		/// Retrieve whether or not autocompletion is hidden automatically when nothing matches.
		/// </summary>
		public bool IsAutoCGetAutoHide
		{
			get 
			{
				return SPerform(2119, 0, 0) != 0;
			}
			set
			{
				SPerform(2118, (uint)(value ? 1 : 0), 0);
			}
		}	

		/// <summary>
		/// Retrieve whether or not autocompletion deletes any word characters
		/// after the inserted text upon completion.
		/// </summary>
		public bool IsAutoCGetDropRestOfWord
		{
			get 
			{
				return SPerform(2271, 0, 0) != 0;
			}
			set
			{
				SPerform(2270, (uint)(value ? 1 : 0), 0);
			}
		}	

		/// <summary>
		/// Retrieve the auto-completion list type-separator character.
		/// </summary>
		public int AutoCTypeSeparator
		{
			get 
			{
				return (int)SPerform(2285, 0, 0);
			}
			set
			{
				SPerform(2286, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Retrieve indentation size.
		/// </summary>
		public int Indent
		{
			get 
			{
				return (int)SPerform(2123, 0, 0);
			}
			set
			{
				SPerform(2122, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Retrieve whether tabs will be used in indentation.
		/// </summary>
		public bool IsUseTabs
		{
			get 
			{
				return SPerform(2125, 0, 0) != 0;
			}
			set
			{
				SPerform(2124 , (uint)(value ? 1 : 0), 0);
			}
		}

		/// <summary>
		/// Is the horizontal scroll bar visible? 
		/// </summary>
		public bool IsHScrollBar
		{
			get 
			{
				return SPerform(2131, 0, 0) != 0;
			}
			set
			{
				SPerform(2130, (uint)(value ? 1 : 0), 0);
			}
		}	

		/// <summary>
		/// Are the indentation guides visible?
		/// </summary>
		public bool IsIndentationGuides
		{
			get 
			{
				return SPerform(2133, 0, 0) != 0;
			}
			set
			{
				SPerform(2132, (uint)(value ? (int)this.indentView : 0), 0);
			}
		}	

		/// <summary>
		/// Get the highlighted indentation guide column.
		/// </summary>
		public int HighlightGuide
		{
			get 
			{
				return (int)SPerform(2135, 0, 0);
			}
			set
			{
				SPerform(2134, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Get the code page used to interpret the bytes of the document as characters.
		/// </summary>
		public int CodePage
		{
			get 
			{
				return (int)SPerform(2137, 0, 0);
			}
			set
			{
				SPerform(2037, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Get the foreground colour of the caret.
		/// </summary>
		public int CaretFore
		{
			get 
			{
				return (int)SPerform(2138, 0, 0);
			}
			set
			{
				SPerform(2069, (uint)value, 0);
			}
		}	

		/// <summary>
		/// In palette mode?
		/// </summary>
		public bool IsUsePalette
		{
			get 
			{
				return SPerform(2139, 0, 0) != 0;
			}
			set
			{
				SPerform(2039, (uint)(value ? 1 : 0), 0);
			}
		}	

		/// <summary>
		/// In read-only mode?
		/// </summary>
		public bool IsReadOnly
		{
			get 
			{
				return SPerform(2140, 0, 0) != 0;
			}
			set
			{
				SPerform(2171, (uint)(value ? 1 : 0), 0);
			}
		}	

		/// <summary>
		/// Returns the position at the start of the selection.
		/// </summary>
		public int SelectionStart
		{
			get 
			{
				return (int)SPerform(2143, 0, 0);
			}
			set
			{
				SPerform(2142, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Returns the position at the end of the selection.
		/// </summary>
		public int SelectionEnd
		{
			get 
			{
				return (int)SPerform(2145, 0, 0);
			}
			set
			{
				SPerform(2144, (uint)value, 0);
			}
		}

        /// <summary>
        /// Returns true if the selection extends over more than one line.
        /// </summary>
        public bool IsSelectionMultiline
        {
            get
            {
                return LineFromPosition(SelectionStart) != LineFromPosition(SelectionEnd);
            }
        }

		/// <summary>
		/// Returns the print magnification.
		/// </summary>
		public int PrintMagnification
		{
			get 
			{
				return (int)SPerform(2147, 0, 0);
			}
			set
			{
				SPerform(2146, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Returns the print colour mode.
		/// </summary>
		public int PrintColourMode
		{
			get 
			{
				return (int)SPerform(2149, 0, 0);
			}
			set
			{
				SPerform(2148, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Retrieve the display line at the top of the display.
		/// </summary>
		public int FirstVisibleLine
		{
			get 
			{
				return (int)SPerform(2152, 0, 0);
			}
		}	

		/// <summary>
		/// Returns the number of lines in the document. There is always at least one.
		/// </summary>
		public int LineCount
		{
			get 
			{
				return (int)SPerform(2154, 0, 0);
			}
		}	

		/// <summary>
		/// Returns the size in pixels of the left margin.
		/// </summary>
		public int MarginLeft
		{
			get 
			{
				return (int)SPerform(2156, 0, 0);
			}
			set
			{
				SPerform(2155, 0, (uint)value);
			}
		}	

		/// <summary>
		/// Returns the size in pixels of the right margin.
		/// </summary>
		public int MarginRight
		{
			get 
			{
				return (int)SPerform(2158, 0, 0);
			}
			set
			{
				SPerform(2157, 0, (uint)value);
			}
		}	

		/// <summary>
		/// Is the document different from when it was last saved?
		/// </summary>	
		public bool IsModify
		{
			get 
			{
				return SPerform(2159, 0, 0) != 0;
			}
		}	

		/// <summary>
		/// Retrieve the number of characters in the document.
		/// </summary>
		public int TextLength
		{
			get 
			{
				return (int)SPerform(2183, 0, 0);
			}
		}	

		/// <summary>
		/// Retrieve a pointer to a function that processes messages for this Scintilla.
		/// </summary>
		public int DirectFunction
		{
			get 
			{
				return (int)SPerform(2184, 0, 0);
			}
		}	

		/// <summary>
		/// Retrieve a pointer value to use as the first argument when calling
		/// the function returned by GetDirectFunction.
		/// </summary>
		public int DirectPointer
		{
			get 
			{
				return (int)SPerform(2185, 0, 0);
			}
		}	

		/// <summary>
		/// Returns true if overtype mode is active otherwise false is returned.
		/// </summary>
		public bool IsOvertype
		{
			get 
			{
				return SPerform(2187, 0, 0) != 0;
			}
			set
			{
				SPerform(2186 , (uint)(value ? 1 : 0), 0);
			}
		}	

		/// <summary>
		/// Returns the width of the insert mode caret.
		/// </summary>
		public int CaretWidth
		{
			get 
			{
				return (int)SPerform(2189, 0, 0);
			}
			set
			{
				SPerform(2188, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Get the position that starts the target. 
		/// </summary>
		public int TargetStart
		{
			get 
			{
				return (int)SPerform(2191, 0, 0);
			}
			set
			{
				SPerform(2190, (uint)value , 0);
			}
		}	

		/// <summary>
		/// Get the position that ends the target.
		/// </summary>
		public int TargetEnd
		{
			get 
			{
				return (int)SPerform(2193, 0, 0);
			}
			set
			{
				SPerform(2192, (uint)value , 0);
			}
		}	

		/// <summary>
		/// Get the search flags used by SearchInTarget.
		/// </summary>
		public int SearchFlags
		{
			get 
			{
				return (int)SPerform(2199, 0, 0);
			}
			set
			{
				SPerform(2198, (uint)value, 0);
			}
		}
		
		/// <summary>
		/// Is a line visible?
		/// </summary>	
		public bool IsLineVisible
		{
			get 
			{
				return SPerform(2228, 0, 0) != 0;
			}
		}

		/// <summary>
		/// Does a tab pressed when caret is within indentation indent?
		/// </summary>
		public bool IsTabIndents
		{
			get 
			{
				return SPerform(2261, 0, 0) != 0;
			}
			set
			{
				SPerform(2260, (uint)(value ? 1 : 0), 0);
			}
		}	

		/// <summary>
		/// Does a backspace pressed when caret is within indentation unindent?
		/// </summary>
		public bool IsBackSpaceUnIndents
		{
			get 
			{
				return SPerform(2263, 0, 0) != 0;
			}
			set
			{
				SPerform(2262, (uint)(value ? 1 : 0), 0);
			}
		}	

		/// <summary>
		/// Retrieve the time the mouse must sit still to generate a mouse dwell event.
		/// </summary>
		public int MouseDwellTime
		{
			get 
			{
				return (int)SPerform(2265, 0, 0);
			}
			set
			{
				SPerform(2264, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Retrieve whether text is word wrapped.
		/// </summary>
		public int WrapMode
		{
			get 
			{
				return (int)SPerform(2269, 0, 0);
			}
			set
			{
				SPerform(2268, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Retrive the display mode of visual flags for wrapped lines.
		/// </summary>
		public int WrapVisualFlags
		{
			get 
			{
				return (int)SPerform(2461, 0, 0);
			}
			set
			{
				SPerform(2460, (uint)value , 0);
			}
		}	

		/// <summary>
		/// Retrive the location of visual flags for wrapped lines.
		/// </summary>
		public int WrapVisualFlagsLocation
		{
			get 
			{
				return (int)SPerform(2463, 0, 0);
			}
			set
			{
				SPerform(2462, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Retrive the start indent for wrapped lines.
		/// </summary>
		public int WrapStartIndent
		{
			get 
			{
				return (int)SPerform(2465, 0, 0);
			}
			set
			{
				SPerform(2464, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Retrieve the degree of caching of layout information.
		/// </summary>
		public int LayoutCache
		{
			get 
			{
				return (int)SPerform(2273, 0, 0);
			}
			set
			{
				SPerform(2272, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Retrieve the document width assumed for scrolling.
		/// </summary>
		public int ScrollWidth
		{
			get 
			{
				return (int)SPerform(2275, 0, 0);
			}
			set
			{
				SPerform(2274, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Retrieve whether the maximum scroll position has the last
		/// line at the bottom of the view.
		/// </summary>
		public int EndAtLastLine
		{
			get 
			{
				return (int)SPerform(2278, 0, 0);
			}
			set
			{
				SPerform(2277, (uint)value , 0);
			}
		}	

		/// <summary>
		/// Is the vertical scroll bar visible?
		/// </summary>
		public bool IsVScrollBar
		{
			get 
			{
				return SPerform(2281, 0, 0) != 0;
			}
			set
			{
				SPerform(2280, (uint)(value ? 1 : 0), 0);
			}
		}	

		/// <summary>
		/// Is drawing done in two phases with backgrounds drawn before faoregrounds?
		/// </summary>
		public bool IsTwoPhaseDraw
		{
			get 
			{
				return SPerform(2283, 0, 0)!=0;
			}
			set
			{
				SPerform(2284, (uint)(value ? 1 : 0), 0);
			}
		}	

		/// <summary>
		/// Are the end of line characters visible?
		/// </summary>
		public bool IsViewEOL
		{
			get 
			{
				return SPerform(2355, 0, 0) != 0;
			}
			set
			{
				SPerform(2356, (uint)(value ? 1 : 0), 0);
			}
		}	

		/// <summary>
		/// Retrieve a pointer to the document object.
		/// </summary>
		public int DocPointer
		{
			get 
			{
				return (int)SPerform(2357, 0, 0);
			}
			set
			{
                SPerform(2358, 0, (uint)value);
			}
		}	

		/// <summary>
		/// Retrieve the column number which text should be kept within.
		/// </summary>
		public int EdgeColumn
		{
			get 
			{
				return (int)SPerform(2360, 0, 0);
			}
			set
			{
				SPerform(2361, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Retrieve the edge highlight mode.
		/// </summary>
		public int EdgeMode
		{
			get 
			{
				return (int)SPerform(2362, 0, 0);
			}
			set
			{
				SPerform(2363, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Retrieve the colour used in edge indication.
		/// </summary>
		public int EdgeColour
		{
			get 
			{
				return (int)SPerform(2364, 0, 0);
			}
			set
			{
				SPerform(2365, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Retrieves the number of lines completely visible.
		/// </summary>
		public int LinesOnScreen
		{
			get 
			{
				return (int)SPerform(2370, 0, 0);
			}
		}	

		/// <summary>
		/// Is the selection rectangular? The alternative is the more common stream selection. 
		/// </summary>	
		public bool IsSelectionRectangle
		{
			get 
			{
				return SPerform(2372, 0, 0) != 0;
			}
		}	

		/// <summary>
		/// Set the zoom level. This number of points is added to the size of all fonts.
		/// It may be positive to magnify or negative to reduce. Retrieve the zoom level.
		/// </summary>
		public int ZoomLevel
		{
			get 
			{
				return (int)SPerform(2374, 0, 0);
			}
			set
			{
				SPerform(2373, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Get which document modification events are sent to the container.
		/// </summary>
		public int ModEventMask
		{
			get 
			{
				return (int)SPerform(2378, 0, 0);
			}
			set
			{
				SPerform(2359, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Change internal focus flag. Get internal focus flag.
		/// </summary>
		public bool IsFocus
		{
			get 
			{
				return SPerform(2381, 0, 0) != 0;
			}
			set
			{
				SPerform(2380 , (uint)(value ? 1 : 0), 0);
			}
		}	

		/// <summary>
		/// Change error status - 0 = OK. Get error status.
		/// </summary>
		public int Status
		{
			get 
			{
				return (int)SPerform(2383, 0, 0);
			}
			set
			{
				SPerform(2382, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Set whether the mouse is captured when its button is pressed. Get whether mouse gets captured.
		/// </summary>
		public bool IsMouseDownCaptures
		{
			get 
			{
				return SPerform(2385, 0, 0) != 0;
			}
			set
			{
				SPerform(2384 , (uint)(value ? 1 : 0), 0);
			}
		}	

		/// <summary>
		/// Sets the cursor to one of the SC_CURSOR/// values. Get cursor type.
		/// </summary>
		public int CursorType
		{
			get 
			{
				return (int)SPerform(2387, 0, 0);
			}
			set
			{
				SPerform(2386, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Change the way control characters are displayed:
		/// If symbol is < 32, keep the drawn way, else, use the given character.
		/// Get the way control characters are displayed.
		/// </summary>
		public int ControlCharSymbol
		{
			get 
			{
				return (int)SPerform(2389, 0, 0);
			}
			set
			{
				SPerform(2388, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Get and Set the xOffset (ie, horizonal scroll position).
		/// </summary>
		public int XOffset
		{
			get 
			{
				return (int)SPerform(2398, 0, 0);
			}
			set
			{
				SPerform(2397, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Is printing line wrapped?
		/// </summary>
		public int PrintWrapMode
		{
			get 
			{
				return (int)SPerform(2407, 0, 0);
			}
			set
			{
				SPerform(2406, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Get the mode of the current selection.
		/// </summary>
		public int SelectionMode
		{
			get 
			{
				return (int)SPerform(2423, 0, 0);
			}
			set
			{
				SPerform(2422, (uint)value, 0);
			}
		}	

		/// <summary>
		/// Retrieve the lexing language of the document.
		/// </summary>
		public int Lexer
		{
			get 
			{
				return (int)SPerform(4002, 0, 0);
			}
			set
			{
				SPerform(4001, (uint)value, 0);
			}
		}

        /// <summary>
        /// Gets the EOL marker
        /// </summary>
        public string NewLineMarker
        {
            get
            {
                if (EOLMode == 1) return "\r";
                else if (EOLMode == 2) return "\n";
                else return "\r\n";
            }
        }

        /// <summary>
        /// Compact the document buffer and return a read-only pointer to the characters in the document.
        /// </summary>
        public int CharacterPointer
        {
            get
            {
                return (int)SPerform(2520, 0, 0);
            }
        }

        /// <summary>
        /// Always interpret keyboard input as Unicode
        /// </summary>
        public bool UnicodeKeys
        {
            get
            {
                return SPerform(2522, 0, 0) != 0;
            }
            set
            {
                SPerform(2521, (uint)(value ? 1 : 0), 0);
            }
        }

        /// <summary>
        /// Set extra ascent for each line
        /// </summary>
        public int ExtraAscent
        {
            get
            {
                return (int)SPerform(2526, 0, 0);
            }
            set
            {
                SPerform(2525, (uint)value, 0);
            }
        }

        /// <summary>
        /// Set extra descent for each line
        /// </summary>
        public int ExtraDescent
        {
            get
            {
                return (int)SPerform(2528, 0, 0);
            }
            set
            {
                SPerform(2527, (uint)value, 0);
            }
        }

        /// <summary>
        /// Get the start of the range of style numbers used for margin text
        /// </summary>
        public int MarginStyleOffset
        {
            get
            {
                return (int)SPerform(2538, 0, 0);
            }
            set
            {
                SPerform(2537, (uint)value, 0);
            }
        }

        /// <summary>
        /// Get the start of the range of style numbers used for annotations
        /// </summary>
        public int AnnotationStyleOffset
        {
            get
            {
                return (int)SPerform(2551, 0, 0);
            }
            set
            {
                SPerform(2550, (uint)value, 0);
            }
        }

        /// <summary>
        /// Get the start of the range of style numbers used for annotations
        /// </summary>
        public bool AnnotationVisible
        {
            get
            {
                return SPerform(2549, 0, 0) != 0;
            }
            set
            {
                SPerform(2548, (uint)(value ? 1 : 0), 0);
            }
        }

        /// <summary>
        /// Sets whether the maximum width line displayed is used to set scroll width.
        /// </summary>
        public bool ScrollWidthTracking
        {
            get
            {
                return SPerform(2517, 0, 0) != 0;
            }
            set
            {
                SPerform(2516, (uint)(value ? 1 : 0), 0);
            }
        }

		#endregion

		#region Scintilla Methods

        /// <summary>
        /// Adds a new keys to ignore
        /// </summary> 
        public virtual void AddIgnoredKeys(Keys keys)
        {
            ignoredKeys.Add((int)keys, (int)keys);
        }

        /// <summary>
        /// Removes the ignored keys
        /// </summary> 
        public virtual void RemoveIgnoredKeys(Keys keys)
        {
            ignoredKeys.Remove((int)keys);
        }

        /// <summary>
        /// Clears the ignored keys container
        /// </summary> 
        public virtual void ClearIgnoredKeys()
        {
            ignoredKeys.Clear();
        }

        /// <summary>
        /// Does the container have keys?
        /// </summary> 
        public virtual bool ContainsIgnoredKeys(Keys keys)
        {
            return ignoredKeys.ContainsKey((int)keys);
        }

        /// <summary>
		/// Sets the focud to the control
		/// </summary>
        public new bool Focus()
        {
            return WinAPI.SetFocus(hwndScintilla) != IntPtr.Zero;
        }

		/// <summary>
		/// Duplicate the selection. 
		/// If selection empty duplicate the line containing the caret.
		/// </summary>
		public void SelectionDuplicate()
		{
			SPerform(2469, 0, 0);
		}
		
		/// <summary>
		/// Can the caret preferred x position only be changed by explicit movement commands?
		/// </summary>
		public bool GetCaretSticky()
		{
			return SPerform(2457, 0, 0) != 0;
		}
		
		/// <summary>
		/// Stop the caret preferred x position changing when the user types.
		/// </summary>
		public void SetCaretSticky(bool useSetting)
		{
			SPerform(2458, (uint)(useSetting ? 1 : 0), 0);
		}
		
		/// <summary>
		/// Switch between sticky and non-sticky: meant to be bound to a key.
		/// </summary>
		public void ToggleCaretSticky()
		{
			SPerform(2459, 0, 0);
		}
		

		/// <summary>
		/// Retrieve the fold level of a line.
		/// </summary>
		public int GetFoldLevel(int line)
		{
			return (int)SPerform(2223, (uint)line, 0);
		}

		/// <summary>
		/// Set the fold level of a line.
		/// This encodes an integer level along with flags indicating whether the
		/// line is a header and whether it is effectively white space.
		/// </summary>
		public void SetFoldLevel(int line, int level)
		{
			SPerform(2222, (uint)line, (uint)level);
		}	

		/// <summary>
		/// Find the last child line of a header line.
		/// </summary>
		public int LastChild(int line, int level)
		{
			return (int)SPerform(2224, (uint)line, (uint)level);
		}	

		/// <summary>
		/// Find the last child line of a header line. 
		/// </summary>
		public int LastChild(int line)
		{
			return (int)SPerform(2224, (uint)line, 0);
		}	

		/// <summary>
		/// Find the parent line of a child line.
		/// </summary>
		public int FoldParent(int line)
		{
			return (int)SPerform(2225, (uint)line, 0);
		}	
		
		/// <summary>
		/// Is a header line expanded?
		/// </summary>
		public bool FoldExpanded(int line)
		{
			return SPerform(2230, (uint)line, 0) != 0;
		}

		/// <summary>
		/// Show the children of a header line.
		/// </summary>
		public void FoldExpanded(int line, bool expanded)
		{
			SPerform(2229, (uint)line, (uint)(expanded ? 1 : 0));
		}	
		
		/// <summary>
		/// Clear all the styles and make equivalent to the global default style.
		/// </summary>
		public void StyleClearAll()
		{
			SPerform(2050, 0, 0);
		}	

		/// <summary>
		/// Set the foreground colour of a style.
		/// </summary>
		public void StyleSetFore(int style, int fore)
		{
			SPerform(2051, (uint)style, (uint)fore);
		}	

		/// <summary>
		/// Set the background colour of a style.
		/// </summary>
		public void StyleSetBack(int style, int back)
		{
			SPerform(2052, (uint)style, (uint)back);
		}	

		/// <summary>
		/// Set a style to be bold or not.
		/// </summary>
		public void StyleSetBold(int style, bool bold)
		{
			SPerform(2053, (uint)style, (uint)(bold ? 1 : 0));
		}	

		/// <summary>
		/// Set a style to be italic or not.
		/// </summary>
		public void StyleSetItalic(int style, bool italic)
		{
			SPerform(2054, (uint)style, (uint)(italic ? 1 : 0));
		}	

		/// <summary>
		/// Set the size of characters of a style.
		/// </summary>
		public void StyleSetSize(int style, int sizePoints)
		{
			SPerform(2055, (uint)style, (uint)sizePoints);
		}	

		/// <summary>
		/// Set the font of a style.
		/// </summary>
		unsafe public void StyleSetFont(int style, string fontName)
		{
			if (fontName == null || fontName.Equals("")) fontName = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(fontName)) 
			{
				SPerform(2056,(uint)style, (uint)b );
			}
		}	
						
		/// <summary>
		/// Set a style to have its end of line filled or not.
		/// </summary>
		public void StyleSetEOLFilled(int style, bool filled)
		{
			SPerform(2057, (uint)style, (uint)(filled ? 1 : 0));
		}	

		/// <summary>
		/// Set a style to be underlined or not.
		/// </summary>
		public void StyleSetUnderline(int style, bool underline )
		{
			SPerform(2059, (uint)style, (uint)(underline?1:0) );
		}	

		/// <summary>
		/// Set a style to be mixed case, or to force upper or lower case.
		/// </summary>
		public void StyleSetCase(int style, int caseForce)
		{
			SPerform(2060, (uint)style, (uint)caseForce);
		}	

		/// <summary>
		/// Set the character set of the font in a style.
		/// </summary>
		public void StyleSetCharacterSet(int style, int characterSet )
		{
			SPerform(2066, (uint)style, (uint)characterSet);
		}	

		/// <summary>
		/// Set a style to be a hotspot or not.
		/// </summary>
		public void StyleSetHotSpot(int style, bool hotspot)
		{
			SPerform(2409, (uint)style, (uint)(hotspot ? 1 : 0));
		}	

		/// <summary>
		/// Set a style to be visible or not.
		/// </summary>
		public void StyleSetVisible(int style, bool visible)
		{
			SPerform(2074, (uint)style, (uint)(visible ? 1 : 0));
		}	

		/// <summary>
		/// Set the set of characters making up words for when moving or selecting by word.
		/// First sets deaults like SetCharsDefault.
		/// </summary>
		unsafe public void WordChars(string characters)
		{
			if (characters == null || characters.Equals("")) characters = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(characters))
			{
				SPerform(2077, 0, (uint)b);
			}
		}					

		/// <summary>
		/// Set a style to be changeable or not (read only).
		/// Experimental feature, currently buggy.
		/// </summary>
		public void StyleSetChangeable(int style, bool changeable )
		{
			SPerform(2099, (uint)style, (uint)(changeable?1:0) );
		}	

		/// <summary>
		/// Define a set of characters that when typed will cause the autocompletion to
		/// choose the selected item.
		/// </summary>
		unsafe public void AutoCSetFillUps(string characterSet )
		{
			if (characterSet == null || characterSet.Equals("")) characterSet = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(characterSet))
			{
				SPerform(2112, 0, (uint)b);
			}
		}	
		
		/// <summary>
		/// Enable / Disable underlining active hotspots.
		/// </summary>
		public void HotspotActiveUnderline(bool useSetting)
		{
			SPerform(2412, (uint)(useSetting ? 1 : 0), 0);
		}
		
		/// <summary>
		/// Limit hotspots to single line so hotspots on two lines don't merge.
		/// </summary>
		public void HotspotSingleLine(bool useSetting)
		{
			SPerform(2421, (uint)(useSetting ? 1 : 0), 0);
		}	
		
		/// <summary>
		/// Set a fore colour for active hotspots.
		/// </summary>
		public void HotspotActiveFore(bool useSetting, int fore)
		{
			SPerform(2410, (uint)(useSetting ? 1 : 0), (uint)fore);
		}	

		/// <summary>
		/// Set a back colour for active hotspots.
		/// </summary>
		public void HotspotActiveBack(bool useSetting, int back)
		{
			SPerform(2411, (uint)(useSetting ? 1 : 0), (uint)back);
		}
		
		/// <summary>
		/// Retrieve the number of bits the current lexer needs for styling.
		/// </summary>
		public int GetStyleBitsNeeded()
		{
			return (int)SPerform(4011, 0, 0);
		}	
						
		/// <summary>
		/// Set up a value that may be used by a lexer for some optional feature.
		/// </summary>
		unsafe public void SetProperty(string key, string val)
		{
			if (key == null || key.Equals("")) key = "\0\0";
			if (val == null || val.Equals("")) val = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(val))
			{
				fixed (byte* b2 = Encoding.GetEncoding(this.CodePage).GetBytes(key))
				{
					SPerform(4004, (uint)b2, (uint)b);
				}
			}
		}
		
		/// <summary>
		/// Retrieve a "property" value previously set with SetProperty,
		/// interpreted as an int AFTER any "$()" variable replacement.
		/// </summary>
		unsafe public int GetPropertyInt(string key)
		{
			if (key == null || key.Equals("")) key = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(key))
			{
				return (int)SPerform(4010, (uint)b, 0);
			}
		}
		
		/// <summary>
		/// Set up the key words used by the lexer.
		/// </summary>
		unsafe public void KeyWords(int keywordSet, string keyWords)
		{
            if (keyWords == null || keyWords.Equals("")) keyWords = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(keyWords))
			{
				SPerform(4005, (uint)keywordSet, (uint)b);
			}
		}	
						
		/// <summary>
		/// Set the lexing language of the document based on string name.
		/// </summary>
		unsafe public void LexerLanguage(string language)
		{
			if (language == null || language.Equals("")) language = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(language))
			{
				SPerform(4006, 0, (uint)b);
			}
		}
		
		/// <summary>
		/// Retrieve the extra styling information for a line.
		/// </summary>
		public int GetLineState(int line)
		{
			return (int)SPerform(2093,(uint)line, 0);
		}

		/// <summary>
		/// Used to hold extra styling information for each line.
		/// </summary>
		public void SetLineState(int line, int state)
		{
			SPerform(2092, (uint)line, (uint)state);
		}
		
		/// <summary>
		/// Retrieve the number of columns that a line is indented.
		/// </summary>
		public int GetLineIndentation(int line)
		{
			return (int)SPerform(2127, (uint)line, 0);
		}

		/// <summary>
		/// Change the indentation of a line to a number of columns.
		/// </summary>
		public void SetLineIndentation(int line, int indentSize)
		{
			SPerform(2126, (uint)line, (uint)indentSize);
		}	

		/// <summary>
		/// Retrieve the position before the first non indentation character on a line.
		/// </summary>
		public int LineIndentPosition(int line)
		{
			return (int)SPerform(2128, (uint)line, 0);
		}	

		/// <summary>
		/// Retrieve the column number of a position, taking tab width into account.
		/// </summary>
		public int Column(int pos)
		{
			return (int)SPerform(2129, (uint)pos, 0);
		}
		
		/// <summary>
		/// Get the position after the last visible characters on a line.
		/// </summary>
		public int LineEndPosition(int line)
		{
			return (int)SPerform(2136, (uint)line, 0);
		}

		/// <summary>
		/// Returns the character byte at the position.
		/// </summary>
		public int CharAt(int pos)
		{
			return (int)SPerform(2007, (uint)pos, 0);
		}	
		
		/// <summary>
		/// Returns the style byte at the position.
		/// </summary>
		public int StyleAt(int pos)
		{
			return (int)SPerform(2010, (uint)pos, 0);
		}	
		
		/// <summary>
		/// Retrieve the type of a margin.
		/// </summary>
		public int GetMarginTypeN(int margin)
		{
			return (int)SPerform(2241, (uint)margin, 0);
		}

		/// <summary>
		/// Set a margin to be either numeric or symbolic.
		/// </summary>
		public void SetMarginTypeN(int margin, int marginType)
		{
			SPerform(2240, (uint)margin, (uint)marginType);
		}	

		/// <summary>
		/// Retrieve the width of a margin in pixels.
		/// </summary>
		public int GetMarginWidthN(int margin)
		{
			return (int)SPerform(2243,(uint)margin, 0);
		}

		/// <summary>
		/// Set the width of a margin to a width expressed in pixels.
		/// </summary>
		public void SetMarginWidthN(int margin, int pixelWidth)
		{
			SPerform(2242, (uint)margin, (uint)pixelWidth);
		}	

		/// <summary>
		/// Retrieve the marker mask of a margin.
		/// </summary>
		public int GetMarginMaskN(int margin)
		{
			return (int)SPerform(2245, (uint)margin, 0);
		}

		/// <summary>
		/// Set a mask that determines which markers are displayed in a margin.
		/// </summary>
		public void SetMarginMaskN(int margin, int mask)
		{
			SPerform(2244, (uint)margin, (uint)mask);
		}	

		/// <summary>
		/// Retrieve the mouse click sensitivity of a margin.
		/// </summary>
		public bool MarginSensitiveN(int margin)
		{
			return SPerform(2247, (uint)margin, 0) != 0;
		}

		/// <summary>
		/// Make a margin sensitive or insensitive to mouse clicks.
		/// </summary>
		public void MarginSensitiveN(int margin, bool sensitive)
		{
			SPerform(2246, (uint)margin, (uint)(sensitive ? 1 : 0));
		}
		
		/// <summary>
		/// Retrieve the style of an indicator.
		/// </summary>
		public int GetIndicStyle(int indic)
		{
			return (int)SPerform(2081, (uint)indic, 0);
		}

		/// <summary>
		/// Set an indicator to plain, squiggle or TT.
		/// </summary>
		public void SetIndicStyle(int indic, int style)
		{
			SPerform(2080, (uint)indic , (uint)style);
		}	

		/// <summary>
		/// Retrieve the foreground colour of an indicator.
		/// </summary>
		public int GetIndicFore(int indic)
		{
			return (int)SPerform(2083,(uint)indic, 0);
		}

		/// <summary>
		/// Set the foreground colour of an indicator.
		/// </summary>
		public void SetIndicFore(int indic, int fore)
		{
			SPerform(2082, (uint)indic, (uint)fore);
		}
		
		/// <summary>
		/// Add text to the document at current position.
		/// </summary>
		unsafe public void AddText(int length, string text )
		{
			if (text == null || text.Equals("")) text = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(text)) 
			{
				 SPerform(2001,(uint)length, (uint)b);
			}
		}			

		/// <summary>
		/// Insert string at a position. 
		/// </summary>
		unsafe public void InsertText(int pos, string text )
		{
			if (text == null || text.Equals("")) text = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(text)) 
			{
				SPerform(2003, (uint)pos, (uint)b);
			}
		}	
						
		/// <summary>
		/// Convert all line endings in the document to one mode.
		/// </summary>
		public void ConvertEOLs(Enums.EndOfLine eolMode)
		{
			ConvertEOLs((int)eolMode);
		}	

		/// <summary>
		/// Set the symbol used for a particular marker number.
		/// </summary>
		public void MarkerDefine(int markerNumber, Enums.MarkerSymbol markerSymbol)
		{
			MarkerDefine(markerNumber, (int)markerSymbol);
		}

		/// <summary>
		/// Set the character set of the font in a style.
		/// </summary>
		public void StyleSetCharacterSet(int style, Enums.CharacterSet characterSet)
		{
			StyleSetCharacterSet(style, (int)characterSet);
		}	

		/// <summary>
		/// Set a style to be mixed case, or to force upper or lower case.
		/// </summary>
		public void StyleSetCase(int style, Enums.CaseVisible caseForce)
		{
			StyleSetCase(style, (int)caseForce);
		}	
		
		/// <summary>
		/// Delete all text in the document.
		/// </summary>
		public void ClearAll()
		{
			SPerform(2004, 0, 0);
		}			

		/// <summary>
		/// Set all style bytes to 0, remove all folding information.
		/// </summary>
		public void ClearDocumentStyle()
		{
			SPerform(2005, 0, 0);
		}	
						
		/// <summary>
		/// Redoes the next action on the undo history.
		/// </summary>
		public void Redo()
		{
			SPerform(2011, 0, 0);
		}	
						
		/// <summary>
		/// Select all the text in the document.
		/// </summary>
		public void SelectAll()
		{
			SPerform(2013, 0, 0);
		}	
						
		/// <summary>
		/// Remember the current position in the undo history as the position
		/// at which the document was saved. 
		/// </summary>
		public void SetSavePoint()
		{
			SPerform(2014, 0, 0);
		}	
					
		/// <summary>
		/// Retrieve the line number at which a particular marker is located.
		/// </summary>
		public int MarkerLineFromHandle(int handle)
		{
			return (int)SPerform(2017, (uint)handle, 0);
		}	
						
		/// <summary>
		/// Delete a marker.
		/// </summary>
		public void MarkerDeleteHandle(int handle)
		{
			 SPerform(2018, (uint)handle, 0);
		}	
						
		/// <summary>
		/// Find the position from a point within the window.
		/// </summary>
		public int PositionFromPoint(int x, int y)
		{
			return (int)SPerform(2022, (uint)x, (uint)y);
		}	
						
		/// <summary>
		/// Find the position from a point within the window but return
		/// INVALID_POSITION if not close to text.
		/// </summary>
		public int PositionFromPointClose(int x, int y)
		{
			return (int)SPerform(2023, (uint)x, (uint)y);
		}	
						
		/// <summary>
		/// Set caret to start of a line and ensure it is visible.
		/// </summary>
		public void GotoLine(int line)
		{
			 SPerform(2024, (uint)line, 0);
		}	
						
		/// <summary>
		/// Set caret to a position and ensure it is visible.
		/// </summary>
		public void GotoPos(int pos)
		{
			 SPerform(2025, (uint)pos, 0);
		}	
						
		/// <summary>
		/// Retrieve the text of the line containing the caret.
		/// Returns the index of the caret on the line.
		/// </summary>
		unsafe public string GetCurLine(int length)
		{
			int sz = (int)SPerform(2027, (uint)length, 0);
			byte[] buffer = new byte[sz+1];
			fixed (byte* b = buffer) SPerform(2027, (uint)length+1, (uint)b);
			return Encoding.GetEncoding(this.CodePage).GetString(buffer, 0, sz-1);
		}

		/// <summary>
		/// Convert all line endings in the document to one mode.
		/// </summary>
		public void ConvertEOLs(int eolMode)
		{
			 SPerform(2029, (uint)eolMode, 0);
		}	
					
		/// <summary>
		/// Set the current styling position to pos and the styling mask to mask.
		/// The styling mask can be used to protect some bits in each styling byte from modification.
		/// </summary>
		public void StartStyling(int pos, int mask)
		{
			 SPerform(2032, (uint)pos, (uint)mask);
		}	
						
		/// <summary>
		/// Change style from current styling position for length characters to a style
		/// and move the current styling position to after this newly styled segment.
		/// </summary>
		public void SetStyling(int length, int style)
		{
			 SPerform(2033, (uint)length, (uint)style);
		}	
						
		/// <summary>
		/// Set the symbol used for a particular marker number.
		/// </summary>
		public void MarkerDefine(int markerNumber, int markerSymbol)
		{
			 SPerform(2040, (uint)markerNumber, (uint)markerSymbol);
		}	
						
		/// <summary>
		/// Set the foreground colour used for a particular marker number.
		/// </summary>
		public void MarkerSetFore(int markerNumber, int fore)
		{
			 SPerform(2041, (uint)markerNumber, (uint)fore);
		}	
						
		/// <summary>
		/// Set the background colour used for a particular marker number.
		/// </summary>
		public void MarkerSetBack(int markerNumber, int back)
		{
			 SPerform(2042, (uint)markerNumber, (uint)back);
		}	
						
		/// <summary>
		/// Add a marker to a line, returning an ID which can be used to find or delete the marker.
		/// </summary>
		public int MarkerAdd(int line, int markerNumber)
		{
			return (int)SPerform(2043, (uint)line, (uint)markerNumber);
		}	
						
		/// <summary>
		/// Delete a marker from a line.
		/// </summary>
		public void MarkerDelete(int line, int markerNumber)
		{
			 SPerform(2044, (uint)line, (uint)markerNumber);
		}	
						
		/// <summary>
		/// Delete all markers with a particular number from all lines.
		/// </summary>
		public void MarkerDeleteAll(int markerNumber)
		{
			 SPerform(2045, (uint)markerNumber, 0);
		}	
						
		/// <summary>
		/// Get a bit mask of all the markers set on a line.
		/// </summary>
		public int MarkerGet(int line)
		{
			return (int)SPerform(2046, (uint)line, 0);
		}	
						
		/// <summary>
		/// Find the next line after lineStart that includes a marker in mask.
		/// </summary>
		public int MarkerNext(int lineStart, int markerMask)
		{
			return (int)SPerform(2047, (uint)lineStart, (uint)markerMask);
		}	
						
		/// <summary>
		/// Find the previous line before lineStart that includes a marker in mask.
		/// </summary>
		public int MarkerPrevious(int lineStart, int markerMask)
		{
			return (int)SPerform(2048, (uint)lineStart, (uint)markerMask);
		}	
						
		/// <summary>
		/// Define a marker from a pixmap.
		/// </summary>
		unsafe public void MarkerDefinePixmap(int markerNumber, string pixmap )
		{
			if (pixmap == null || pixmap.Equals("")) pixmap = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(pixmap))
			{
				 SPerform(2049, (uint)markerNumber, (uint)b);
			}	
		}			

		/// <summary>
		/// Reset the default style to its state at startup
		/// </summary>
		public void StyleResetDefault()
		{
			SPerform(2058, 0, 0);
		}	
						
		/// <summary>
		/// Set the foreground colour of the selection and whether to use this setting.
		/// </summary>
		public void SetSelFore(bool useSetting, int fore)
		{
			 SPerform(2067,(uint)(useSetting ? 1 : 0), (uint)fore);
		}	
						
		/// <summary>
		/// Set the background colour of the selection and whether to use this setting.
		/// </summary>
		public void SetSelBack(bool useSetting, int back)
		{
			 SPerform(2068, (uint)(useSetting ? 1 : 0), (uint)back);
		}	
						
		/// <summary>
		/// When key+modifier combination km is pressed perform msg.
		/// </summary>
		public void AssignCmdKey(int km, int msg)
		{
			 SPerform(2070, (uint)km, (uint)msg);
		}	
						
		/// <summary>
		/// When key+modifier combination km is pressed do nothing.
		/// </summary>
		public void ClearCmdKey(int km)
		{
			 SPerform(2071, (uint)km, 0);
		}	
						
		/// <summary>
		/// Drop all key mappings.
		/// </summary>
		public void ClearAllCmdKeys()
		{
			SPerform(2072, 0, 0);
		}	
						
		/// <summary>
		/// Set the styles for a segment of the document.
		/// </summary>
		unsafe public void SetStylingEx(int length, string styles)
		{
			if (styles == null || styles.Equals("")) styles = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(styles))
			{
				 SPerform(2073,(uint)length, (uint)b);
			}
		}	
						
		/// <summary>
		/// Start a sequence of actions that is undone and redone as a unit.
		/// May be nested.
		/// </summary>
		public void BeginUndoAction()
		{
			SPerform(2078, 0, 0);
		}	
						
		/// <summary>
		/// End a sequence of actions that is undone and redone as a unit.
		/// </summary>
		public void EndUndoAction()
		{
			SPerform(2079, 0, 0);
		}	
						
		/// <summary>
		/// Set the foreground colour of all whitespace and whether to use this setting.
		/// </summary>
		public void SetWhitespaceFore(bool useSetting, int fore)
		{
			 SPerform(2084, (uint)(useSetting ? 1 : 0), (uint)fore);
		}	
						
		/// <summary>
		/// Set the background colour of all whitespace and whether to use this setting.
		/// </summary>
		public void SetWhitespaceBack(bool useSetting, int back)
		{
			 SPerform(2085, (uint)(useSetting ? 1 : 0), (uint)back);
		}	
						
		/// <summary>
		/// Display a auto-completion list.
		/// The lenEntered parameter indicates how many characters before
		/// the caret should be used to provide context.
		/// </summary>
		unsafe public void AutoCShow(int lenEntered, string itemList)
		{
			if (itemList == null || itemList.Equals("")) itemList = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(itemList))
			{
				SPerform(2100, (uint)lenEntered, (uint)b);
			}
		}	
						
		/// <summary>
		/// Remove the auto-completion list from the screen.
		/// </summary>
		public void AutoCCancel()
		{
			SPerform(2101, 0, 0);
		}	
						
		/// <summary>
		/// User has selected an item so remove the list and insert the selection.
		/// </summary>
		public void AutoCComplete()
		{
			SPerform(2104, 0, 0);
		}	
						
		/// <summary>
		/// Define a set of character that when typed cancel the auto-completion list.
		/// </summary>
		unsafe public void AutoCStops(string characterSet)
		{
			if (characterSet == null || characterSet.Equals("")) characterSet = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(characterSet))
			{
				 SPerform(2105, 0, (uint)b);
			}
		}	
						
		/// <summary>
		/// Select the item in the auto-completion list that starts with a string.
		/// </summary>
		unsafe public void AutoCSelect(string text)
		{
			if (text == null || text.Equals("")) text = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(text))
			{
				 SPerform(2108, 0, (uint)b);
			}
		}	
						
		/// <summary>
		/// Display a list of strings and send notification when user chooses one.
		/// </summary>
		unsafe public void UserListShow(int listType, string itemList)
		{
			if (itemList == null || itemList.Equals("")) itemList = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(itemList))
			{
				 SPerform(2117, (uint)listType, (uint)b);
			}
		}	
						
		/// <summary>
		/// Register an XPM image for use in autocompletion lists.
		/// </summary>
		unsafe public void RegisterImage(int type, string xpmData)
		{
			if (xpmData == null || xpmData.Equals("")) xpmData = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(xpmData)) 
			{
				 SPerform(2405,(uint)type, (uint)b);
			}
		}	
						
		/// <summary>
		/// Clear all the registered XPM images.
		/// </summary>
		public void ClearRegisteredImages()
		{
			SPerform(2408, 0, 0);
		}	
						
		/// <summary>
		/// Retrieve the contents of a line.
		/// </summary>
		unsafe public string GetLine(int line)
		{
			try 
			{
				int sz = (int)SPerform(2153, (uint)line, 0);
				byte[] buffer = new byte[sz + 1];
				fixed (byte* b = buffer) SPerform(2153, (uint)line, (uint)b);
                if (buffer[sz - 1] == 10) sz--;
				return Encoding.GetEncoding(this.CodePage).GetString(buffer, 0, sz);
			} 
			catch 
			{
				return "";
			}
		}

		/// <summary>
		/// Select a range of text.
		/// </summary>
		public void SetSel(int start, int end)
		{ 
			SPerform(2160, (uint)start, (uint)end);
		}					

		/// <summary>
		/// Retrieve the selected text.
		/// Return the length of the text.
		/// </summary>
		unsafe public string SelText
		{
			get
			{
				int sz = (int)SPerform(2161,0 ,0);
				byte[] buffer = new byte[sz+1];
				fixed (byte* b = buffer)
				{
					SPerform(2161, (UInt32)sz + 1, (uint)b);
				}
				return Encoding.GetEncoding(this.CodePage).GetString(buffer, 0, sz-1);
			}
		}

		/// <summary>
		/// Draw the selection in normal style or with selection highlighted.
		/// </summary>
		public void HideSelection(bool normal)
		{
			 SPerform(2163, (uint)(normal ? 1 : 0), 0);
		}	
						
		/// <summary>
		/// Retrieve the x value of the point in the window where a position is displayed.
		/// </summary>
		public int PointXFromPosition(int pos)
		{
			return (int) SPerform(2164, 0, (uint)pos);
		}	
						
		/// <summary>
		/// Retrieve the y value of the point in the window where a position is displayed.
		/// </summary>
		public int PointYFromPosition(int pos)
		{
			return (int) SPerform(2165, 0, (uint)pos);
		}	
						
		/// <summary>
		/// Retrieve the line containing a position.
		/// </summary>
		public int LineFromPosition(int pos)
		{
			return (int) SPerform(2166, (uint)pos, 0);
		}	
						
		/// <summary>
		/// Retrieve the position at the start of a line.
		/// </summary>
		public int PositionFromLine(int line)
		{
			return (int) SPerform(2167, (uint)line, 0);
		}
		
		/// <summary>
		/// Retrieve the text from line before position
		/// </summary>
        public String GetLineUntilPosition(int pos)
        {
            int curLine = LineFromPosition(pos);
            int curPosInLine = pos - PositionFromLine(curLine);
            String line = GetLine(curLine);
            int length = MBSafeLengthFromBytes(line, curPosInLine);
            String lineUntilPos = line.Substring(0, length);
            return lineUntilPos;
        }
						
		/// <summary>
		/// Scroll horizontally and vertically.
		/// </summary>
		public void LineScroll(int columns, int lines)
		{
			 SPerform(2168, (uint)columns, (uint)lines);
		}	
						
		/// <summary>
		/// Ensure the caret is visible.
		/// </summary>
		public void ScrollCaret()
		{
			SPerform(2169, 0, 0);
		}	
					
		/// <summary>
		/// Replace the selected text with the argument text.
		/// </summary>
		unsafe public void ReplaceSel(string text)
		{
			if (text == null || text.Equals("")) text = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(text)) 
			{
				SPerform(2170,0 , (uint)b);
			}
		}	
						
		/// <summary>
		/// Null operation.
		/// </summary>
		public void Null()
		{
			SPerform(2172, 0, 0);
		}	
						
		/// <summary>
		/// Delete the undo history.
		/// </summary>
		public void EmptyUndoBuffer()
		{
			SPerform(2175, 0, 0);
		}	
						
		/// <summary>
		/// Undo one action in the undo history.
		/// </summary>
		public void Undo()
		{
			SPerform(2176, 0, 0);
		}	
						
		/// <summary>
		/// Cut the selection to the clipboard.
		/// </summary>
		public void Cut()
		{
			SPerform(2177, 0, 0);
		}	
						
		/// <summary>
		/// Copy the selection to the clipboard.
		/// </summary>
		public void Copy()
		{
			SPerform(2178, 0, 0);
            // Invoke UI update after copy...
            if (UpdateUI != null) UpdateUI(this);
		}

        /// <summary>
        /// Copy the selection to the clipboard as RTF.
        /// </summary>
        public void CopyRTF()
        {
            Language language = ScintillaControl.Configuration.GetLanguage(this.configLanguage);
            String conversion = RTF.GetConversion(language, this, this.SelectionStart, this.SelectionEnd);
            Clipboard.SetText(conversion, TextDataFormat.Rtf);
        }
			
		/// <summary>
		/// Paste the contents of the clipboard into the document replacing the selection.
		/// </summary>
		public void Paste()
		{
			SPerform(2179, 0, 0);
		}	
						
		/// <summary>
		/// Clear the selection.
		/// </summary>
		public void Clear()
		{
			SPerform(2180, 0, 0);
		}	
						
		/// <summary>
		/// Replace the contents of the document with the argument text.
		/// </summary>
		unsafe public void SetText(string text)
		{
			if (text == null || text.Equals("")) text = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(text))
			{
				SPerform(2181, 0, (uint)b);
			}
		}	
						
		/// <summary>
		/// Retrieve all the text in the document. Returns number of characters retrieved.
		/// </summary>
		unsafe public string GetText(int length)
		{
			int sz = (int)SPerform(2182, (uint)length, 0);
			byte[] buffer = new byte[sz+1];
			fixed (byte* b = buffer)SPerform(2182, (uint)length+1, (uint)b);
			return Encoding.GetEncoding(this.CodePage).GetString(buffer, 0, sz-1);
		}

		/// <summary>
		/// Replace the target text with the argument text.
		/// Text is counted so it can contain NULs.
		/// Returns the length of the replacement text.
		/// </summary>
		unsafe public int ReplaceTarget(int length, string text)
		{
			if (text == null || text.Equals("")) text = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(text))
			{
				return (int)SPerform(2194, (uint)length, (uint)b);
			}
		}	
						
		/// <summary>
		/// Replace the target text with the argument text after \d processing.
		/// Text is counted so it can contain NULs.
		/// Looks for \d where d is between 1 and 9 and replaces these with the strings
		/// matched in the last search operation which were surrounded by \( and \).
		/// Returns the length of the replacement text including any change
		/// caused by processing the \d patterns.
		/// </summary>
		unsafe public int ReplaceTargetRE(int length, string text )
		{
			if (text == null || text.Equals("")) text = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(text))
			{
				return (int) SPerform(2195, (uint)length, (uint)b);
			}
		}	
						
		/// <summary>
		/// Search for a counted string in the target and set the target to the found
		/// range. Text is counted so it can contain NULs.
		/// Returns length of range or -1 for failure in which case target is not moved.
		/// </summary>
		unsafe public int SearchInTarget(int length, string text)
		{
			if (text == null || text.Equals("")) text = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(text)) 
			{
				return (int) SPerform(2197, (uint)length, (uint)b);
			}
		}				

		/// <summary>
		/// Show a call tip containing a definition near position pos.
		/// </summary>
		unsafe public void CallTipShow(int pos, string definition)
		{
			if (definition == null || definition.Equals("")) definition = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(definition)) 
			{
				SPerform(2200, (uint)pos, (uint)b);
			}
		}				

		/// <summary>
		/// Remove the call tip from the screen.
		/// </summary>
		public void CallTipCancel()
		{
			SPerform(2201, 0, 0);
		}	
		
		/// <summary>
		/// Highlight a segment of the definition.
		/// </summary>
		public void CallTipSetHlt(int start, int end)
		{
			 SPerform(2204, (uint)start, (uint)end);
		}	
		
		/// <summary>
		/// Set the background colour for the call tip.
		/// </summary>
		public void CallTipSetBack(int color)
		{
			SPerform(2205, (uint)color, 0); 
		}
		
		/// <summary>
		/// Set the foreground colour for the call tip.
		/// </summary>
		public void CallTipSetFore(int color)
		{
			SPerform(2206, (uint)color, 0);
		}
		
		/// <summary>
		/// Set the foreground colour for the highlighted part of the call tip.
		/// </summary>
		public void CallTipSetForeHlt(int color)
		{
			SPerform(2207, (uint)color, 0); 
		}
		
		/// <summary>
		/// Find the display line of a document line taking hidden lines into account.
		/// </summary>
		public int VisibleFromDocLine(int line)
		{
			return (int)SPerform(2220, (uint)line, 0);
		}	

		/// <summary>
		/// Find the document line of a display line taking hidden lines into account.
		/// </summary>
		public int DocLineFromVisible(int lineDisplay)
		{
			return (int)SPerform(2221, (uint)lineDisplay, 0);
		}			

		/// <summary>
		/// Make a range of lines visible.
		/// </summary>
		public void ShowLines(int lineStart, int lineEnd)
		{
			 SPerform(2226, (uint)lineStart, (uint)lineEnd);
		}	
		
		/// <summary>
		/// Make a range of lines invisible.
		/// </summary>
		public void HideLines(int lineStart, int lineEnd)
		{
			 SPerform(2227, (uint)lineStart, (uint)lineEnd);
		}			

		/// <summary>
		/// Switch a header line between expanded and contracted.
		/// </summary>
		public void ToggleFold(int line)
		{
			 SPerform(2231, (uint)line, 0);
		}			

		/// <summary>
		/// Ensure a particular line is visible by expanding any header line hiding it.
		/// </summary>
		public void EnsureVisible(int line)
		{
			 SPerform(2232, (uint)line, 0);
		}				

		/// <summary>
		/// Set some style options for folding.
		/// </summary>
		public void SetFoldFlags(int flags)
		{
			 SPerform(2233, (uint)flags, 0);
		}			

		/// <summary>
		/// Ensure a particular line is visible by expanding any header line hiding it.
		/// Use the currently set visibility policy to determine which range to display.
		/// </summary>
		public void EnsureVisibleEnforcePolicy(int line)
		{
			 SPerform(2234, (uint)line, 0);
		}			

		/// <summary>
		/// Get position of start of word.
		/// </summary>
		public int WordStartPosition(int pos, bool onlyWordCharacters)
		{
			return (int)SPerform(2266, (uint)pos, (uint)(onlyWordCharacters ? 1 : 0));
		}				

		/// <summary>
		/// Get position of end of word.
		/// </summary>
		public int WordEndPosition(int pos, bool onlyWordCharacters)
		{
			return (int)SPerform(2267, (uint)pos, (uint)(onlyWordCharacters ? 1 : 0));
		}			

		/// <summary>
		/// Measure the pixel width of some text in a particular style.
		/// NUL terminated text argument.
		/// Does not handle tab or control characters.
		/// </summary>
		unsafe public int TextWidth(int style, string text)
		{
			if (text == null || text.Equals("")) text = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(text)) 
			{
				return (int)SPerform(2276, (uint)style, (uint)b);
			}
		}				

		/// <summary>
		/// Retrieve the height of a particular line of text in pixels.
		/// </summary>
		public int TextHeight(int line)
		{
			return (int) SPerform(2279, (uint)line, 0);
		}	
						
		/// <summary>
		/// Append a string to the end of the document without changing the selection.
		/// </summary>
		unsafe public void AppendText(int length, string text)
		{
			if (text == null || text.Equals("")) text = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(text))
			{
				SPerform(2282, (uint)length, (uint)b);
			}
		}	
						
		/// <summary>
		/// Make the target range start and end be the same as the selection range start and end.
		/// </summary>
		public void TargetFromSelection()
		{
			SPerform(2287, 0, 0);
		}	
						
		/// <summary>
		/// Join the lines in the target.
		/// </summary>
		public void LinesJoin()
		{
			SPerform(2288, 0, 0);
		}	
						
		/// <summary>
		/// Split the lines in the target into lines that are less wide than pixelWidth
		/// where possible.
		/// </summary>
		public void LinesSplit(int pixelWidth)
		{
			 SPerform(2289, (uint)pixelWidth, 0);
		}	
						
		/// <summary>
		/// Set the colours used as a chequerboard pattern in the fold margin
		/// </summary>
		public void SetFoldMarginColour(bool useSetting, int back)
		{
			 SPerform(2290,(uint)(useSetting ? 1 : 0), (uint)back);
		}	
						
		/// <summary>
		/// Set the colours used as a chequerboard pattern in the fold margin
		/// </summary>
		public void SetFoldMarginHiColour(bool useSetting, int fore)
		{
			 SPerform(2291,(uint)(useSetting ? 1 : 0), (uint)fore);
		}	
						
		/// <summary>
		/// Move caret down one line.
		/// </summary>
		public void LineDown()
		{
			SPerform(2300, 0, 0);
		}	
						
		/// <summary>
		/// Move caret down one line extending selection to new caret position.
		/// </summary>
		public void LineDownExtend()
		{
			SPerform(2301, 0, 0);
		}	
						
		/// <summary>
		/// Move caret up one line.
		/// </summary>
		public void LineUp()
		{
			SPerform(2302, 0, 0);
		}	
						
		/// <summary>
		/// Move caret up one line extending selection to new caret position.
		/// </summary>
		public void LineUpExtend()
		{
			SPerform(2303, 0, 0);
		}	
						
		/// <summary>
		/// Move caret left one character.
		/// </summary>
		public void CharLeft()
		{
			SPerform(2304, 0, 0);
		}	
						
		/// <summary>
		/// Move caret left one character extending selection to new caret position.
		/// </summary>
		public void CharLeftExtend()
		{
			SPerform(2305, 0, 0);
		}	
						
		/// <summary>
		/// Move caret right one character.
		/// </summary>
		public void CharRight()
		{
			SPerform(2306, 0, 0);
		}	
						
		/// <summary>
		/// Move caret right one character extending selection to new caret position.
		/// </summary>
		public void CharRightExtend()
		{
			SPerform(2307, 0, 0);
		}	
						
		/// <summary>
		/// Move caret left one word.
		/// </summary>
		public void WordLeft()
		{
			SPerform(2308, 0, 0);
		}	
						
		/// <summary>
		/// Move caret left one word extending selection to new caret position.
		/// </summary>
		public void WordLeftExtend()
		{
			SPerform(2309, 0, 0);
		}	
						
		/// <summary>
		/// Move caret right one word.
		/// </summary>
		public void WordRight()
		{
			SPerform(2310, 0, 0);
		}	
						
		/// <summary>
		/// Move caret right one word extending selection to new caret position.
		/// </summary>
		public void WordRightExtend()
		{
			SPerform(2311, 0, 0);
		}	
						
		/// <summary>
		/// Move caret to first position on line.
		/// </summary>
		public void Home()
		{
			SPerform(2312, 0, 0);
		}	
						
		/// <summary>
		/// Move caret to first position on line extending selection to new caret position.
		/// </summary>
		public void HomeExtend()
		{
			SPerform(2313, 0, 0);
		}	
						
		/// <summary>
		/// Move caret to last position on line.
		/// </summary>
		public void LineEnd()
		{
			SPerform(2314, 0, 0);
		}	
						
		/// <summary>
		/// Move caret to last position on line extending selection to new caret position.
		/// </summary>
		public void LineEndExtend()
		{
			SPerform(2315, 0, 0);
		}	
						
		/// <summary>
		/// Move caret to first position in document.
		/// </summary>
		public void DocumentStart()
		{
			SPerform(2316, 0, 0);
		}	
						
		/// <summary>
		/// Move caret to first position in document extending selection to new caret position.
		/// </summary>
		public void DocumentStartExtend()
		{
			SPerform(2317, 0, 0);
		}	
						
		/// <summary>
		/// Move caret to last position in document.
		/// </summary>
		public void DocumentEnd()
		{
			SPerform(2318, 0, 0);
		}	
						
		/// <summary>
		/// Move caret to last position in document extending selection to new caret position.
		/// </summary>
		public void DocumentEndExtend()
		{
			SPerform(2319, 0, 0);
		}	
						
		/// <summary>
		/// Move caret one page up.
		/// </summary>
		public void PageUp()
		{
			SPerform(2320, 0, 0);
		}	
						
		/// <summary>
		/// Move caret one page up extending selection to new caret position.
		/// </summary>
		public void PageUpExtend()
		{
			SPerform(2321, 0, 0);
		}	
						
		/// <summary>
		/// Move caret one page down.
		/// </summary>
		public void PageDown()
		{
			SPerform(2322, 0, 0);
		}	
						
		/// <summary>
		/// Move caret one page down extending selection to new caret position.
		/// </summary>
		public void PageDownExtend()
		{
			SPerform(2323, 0, 0);
		}	
						
		/// <summary>
		/// Switch from insert to overtype mode or the reverse.
		/// </summary>
		public void EditToggleOvertype()
		{
			SPerform(2324, 0, 0);
		}	
						
		/// <summary>
		/// Cancel any modes such as call tip or auto-completion list display.
		/// </summary>
		public void Cancel()
		{
			SPerform(2325, 0, 0);
		}	
						
		/// <summary>
		/// Delete the selection or if no selection, the character before the caret.
		/// </summary>
		public void DeleteBack()
		{
			SPerform(2326, 0, 0);
		}

        /// <summary>
        /// Delete the character after the caret.
        /// </summary>
        public void DeleteForward()
        {
            SetSel(CurrentPos + 1, CurrentPos + 1);
            DeleteBack();
        }
						
		/// <summary>
		/// If selection is empty or all on one line replace the selection with a tab character.
		/// If more than one line selected, indent the lines.
		/// </summary>
		public void Tab()
		{
			SPerform(2327, 0, 0);
		}	
						
		/// <summary>
		/// Dedent the selected lines.
		/// </summary>
		public void BackTab()
		{
			SPerform(2328, 0, 0);
		}	
						
		/// <summary>
		/// Insert a new line, may use a CRLF, CR or LF depending on EOL mode.
		/// </summary>
		public void NewLine()
		{
			SPerform(2329, 0, 0);
		}	
						
		/// <summary>
		/// Insert a Form Feed character.
		/// </summary>
		public void FormFeed()
		{
			SPerform(2330, 0, 0);
		}	
						
		/// <summary>
		/// Move caret to before first visible character on line.
		/// If already there move to first character on line.
		/// </summary>
		public void VCHome()
		{
			SPerform(2331, 0, 0);
		}	
						
		/// <summary>
		/// Like VCHome but extending selection to new caret position.
		/// </summary>
		public void VCHomeExtend()
		{
			SPerform(2332, 0, 0);
		}	
						
		/// <summary>
		/// Magnify the displayed text by increasing the sizes by 1 point.
		/// </summary>
		public void ZoomIn()
		{
			SPerform(2333, 0, 0);
		}	
						
		/// <summary>
		/// Make the displayed text smaller by decreasing the sizes by 1 point.
		/// </summary>
		public void ZoomOut()
		{
			SPerform(2334, 0, 0);
		}	
		
        /// <summary>
        /// Reset the text zooming by setting zoom level to 0.
        /// </summary>
		public void ResetZoom()
        {
            SPerform(2373, 0, 0);
        }

		/// <summary>
		/// Delete the word to the left of the caret.
		/// </summary>
		public void DelWordLeft()
		{
			SPerform(2335, 0, 0);
		}	
						
		/// <summary>
		/// Delete the word to the right of the caret.
		/// </summary>
		public void DelWordRight()
		{
			SPerform(2336, 0, 0);
		}	
						
		/// <summary>
		/// Cut the line containing the caret.
		/// </summary>
		public void LineCut()
		{
			SPerform(2337, 0, 0);
		}	
						
		/// <summary>
		/// Delete the line containing the caret.
		/// </summary>
		public void LineDelete()
		{
			SPerform(2338, 0, 0);
		}	
						
		/// <summary>
		/// Switch the current line with the previous.
		/// </summary>
		public void LineTranspose()
		{
			SPerform(2339, 0, 0);
		}	
						
		/// <summary>
		/// Duplicate the current line.
		/// </summary>
		public void LineDuplicate()
		{
			SPerform(2404, 0, 0);
		}	
						
		/// <summary>
		/// Transform the selection to lower case.
		/// </summary>
		public void LowerCase()
		{
            SPerform(2340, 0, 0);
		}	
						
		/// <summary>
		/// Transform the selection to upper case.
		/// </summary>
		public void UpperCase()
		{
            SPerform(2341, 0, 0);
		}	
						
		/// <summary>
		/// Scroll the document down, keeping the caret visible.
		/// </summary>
		public void LineScrollDown()
		{
			SPerform(2342, 0, 0);
		}	
						
		/// <summary>
		/// Scroll the document up, keeping the caret visible.
		/// </summary>
		public void LineScrollUp()
		{
			SPerform(2343, 0, 0);
		}	
						
		/// <summary>
		/// Delete the selection or if no selection, the character before the caret.
		/// Will not delete the character before at the start of a line.
		/// </summary>
		public void DeleteBackNotLine()
		{
			SPerform(2344, 0, 0);
		}	
					
		/// <summary>
		/// Move caret to first position on display line.
		/// </summary>
		public void HomeDisplay()
		{
			SPerform(2345, 0, 0);
		}	
						
		/// <summary>
		/// Move caret to first position on display line extending selection to
		/// new caret position.
		/// </summary>
		public void HomeDisplayExtend()
		{
			SPerform(2346, 0, 0);
		}	
						
		/// <summary>
		/// Move caret to last position on display line.
		/// </summary>
		public void LineEndDisplay()
		{
			SPerform(2347, 0, 0);
		}	
						
		/// <summary>
		/// Move caret to last position on display line extending selection to new
		/// caret position.
		/// </summary>
		public void LineEndDisplayExtend()
		{
			SPerform(2348, 0, 0);
		}	
						
		/// <summary>
		/// </summary>
		public void HomeWrap()
		{
			SPerform(2349, 0, 0);
		}	
						
		/// <summary>
		/// </summary>
		public void HomeWrapExtend()
		{
			SPerform(2450, 0, 0);
		}	
						
		/// <summary>
		/// </summary>
		public void LineEndWrap()
		{
			SPerform(2451, 0, 0);
		}	
						
		/// <summary>
		/// </summary>
		public void LineEndWrapExtend()
		{
			SPerform(2452, 0, 0);
		}	
						
		/// <summary>
		/// </summary>
		public void VCHomeWrap()
		{
			SPerform(2453, 0, 0);
		}	
						
		/// <summary>
		/// </summary>
		public void VCHomeWrapExtend()
		{
			SPerform(2454, 0, 0);
		}	
						
		/// <summary>
		/// Copy the line containing the caret.
		/// </summary>
		public void LineCopy()
		{
			SPerform(2455, 0, 0);
		}	
						
		/// <summary>
		/// Move the caret inside current view if it's not there already.
		/// </summary>
		public void MoveCaretInsideView()
		{
			SPerform(2401, 0, 0);
		}	
						
		/// <summary>
		/// How many characters are on a line, not including end of line characters?
		/// </summary>
		public int LineLength(int line)
		{
			return (int)SPerform(2350, (uint)line, 0);
		}	
						
		/// <summary>
		/// Highlight the characters at two positions.
		/// </summary>
		public void BraceHighlight(int pos1, int pos2)
		{
			 SPerform(2351, (uint)pos1, (uint)pos2);
		}	
						
		/// <summary>
		/// Highlight the character at a position indicating there is no matching brace.
		/// </summary>
		public void BraceBadLight(int pos)
		{
			 SPerform(2352, (uint)pos, 0);
		}	
						
		/// <summary>
		/// Find the position of a matching brace or INVALID_POSITION if no match.
		/// </summary>
		public int BraceMatch(int pos)
		{
			return (int)SPerform(2353, (uint)pos, 0);
		}	
						
		/// <summary>
		/// Sets the current caret position to be the search anchor.
		/// </summary>
		public void SearchAnchor()
		{
			SPerform(2366, 0, 0);
		}	
						
		/// <summary>
		/// Find some text starting at the search anchor.
		/// Does not ensure the selection is visible.
		/// </summary>
		unsafe public int SearchNext(int flags, string text)
		{
			if (text == null || text.Equals("")) text = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(text)) 
			{
				return (int)SPerform(2367, (uint)flags, (uint)b);
			}
		}	
						
		/// <summary>
		/// Find some text starting at the search anchor and moving backwards.
		/// Does not ensure the selection is visible.
		/// </summary>
		unsafe public int SearchPrev(int flags, string text)
		{
			if (text == null || text.Equals("")) text = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(text)) 
			{
				return (int)SPerform(2368,(uint)flags, (uint)b);
			}
		}	
						
		/// <summary>
		/// Set whether a pop up menu is displayed automatically when the user presses
		/// the wrong mouse button.
		/// </summary>
		public void UsePopUp(bool allowPopUp)
		{
			 SPerform(2371, (uint)(allowPopUp ? 1 : 0), 0);
		}	
						
		/// <summary>
		/// Create a new document object.
		/// Starts with reference count of 1 and not selected into editor.
		/// Extend life of document.
		/// </summary>
		public void AddRefDocument(int doc)
		{
			 SPerform(2376, 0, (uint)doc );
		}	
						
		/// <summary>
		/// Release a reference to the document, deleting document if it fades to black.
		/// </summary>
		public void ReleaseDocument(int doc)
		{
			 SPerform(2377, 0, (uint)doc );
		}	
					
		/// <summary>
		/// Move to the previous change in capitalisation.
		/// </summary>
		public void WordPartLeft()
		{
			SPerform(2390, 0, 0);
		}	
						
		/// <summary>
		/// Move to the previous change in capitalisation extending selection
		/// to new caret position.
		/// </summary>
		public void WordPartLeftExtend()
		{
			SPerform(2391, 0, 0);
		}	
						
		/// <summary>
		/// Move to the change next in capitalisation.
		/// </summary>
		public void WordPartRight()
		{
			SPerform(2392, 0, 0);
		}	
						
		/// <summary>
		/// Move to the next change in capitalisation extending selection
		/// to new caret position.
		/// </summary>
		public void WordPartRightExtend()
		{
			SPerform(2393, 0, 0);
		}	
					
		/// <summary>
		/// Constants for use with SetVisiblePolicy, similar to SetCaretPolicy.
		/// Set the way the display area is determined when a particular line
		/// is to be moved to by Find, FindNext, GotoLine, etc.
		/// </summary>
		public void SetVisiblePolicy(int visiblePolicy, int visibleSlop)
		{
			 SPerform(2394, (uint)visiblePolicy, (uint)visibleSlop);
		}	
						
		/// <summary>
		/// Delete back from the current position to the start of the line.
		/// </summary>
		public void DelLineLeft()
		{
			SPerform(2395, 0, 0);
		}	
						
		/// <summary>
		/// Delete forwards from the current position to the end of the line.
		/// </summary>
		public void DelLineRight()
		{
			SPerform(2396, 0, 0);
		}	
						
		/// <summary>
		/// Set the last x chosen value to be the caret x position.
		/// </summary>
		public void ChooseCaretX()
		{
			SPerform(2399, 0, 0);
		}	
						
		/// <summary>
		/// Set the focus to this Scintilla widget.
		/// GTK+ Specific.
		/// </summary>
		public void GrabFocus()
		{
			SPerform(2400, 0, 0);
		}	
						
		/// <summary>
		/// Set the way the caret is kept visible when going sideway.
		/// The exclusion zone is given in pixels.
		/// </summary>
		public void SetXCaretPolicy(int caretPolicy, int caretSlop)
		{
			 SPerform(2402, (uint)caretPolicy, (uint)caretSlop);
		}	
						
		/// <summary>
		/// Set the way the line the caret is on is kept visible.
		/// The exclusion zone is given in lines.
		/// </summary>
		public void SetYCaretPolicy(int caretPolicy, int caretSlop)
		{
			 SPerform(2403, (uint)caretPolicy, (uint)caretSlop);
		}	
						
		/// <summary>
		/// Move caret between paragraphs (delimited by empty lines).
		/// </summary>
		public void ParaDown()
		{
			SPerform(2413, 0, 0);
		}	
						
		/// <summary>
		/// Move caret between paragraphs (delimited by empty lines).
		/// </summary>
		public void ParaDownExtend()
		{
			SPerform(2414, 0, 0);
		}	
						
		/// <summary>
		/// Move caret between paragraphs (delimited by empty lines).
		/// </summary>
		public void ParaUp()
		{
			SPerform(2415, 0, 0);
		}	
						
		/// <summary>
		/// Move caret between paragraphs (delimited by empty lines).
		/// </summary>
		public void ParaUpExtend()
		{
			SPerform(2416, 0, 0);
		}	
						
		/// <summary>
		/// Given a valid document position, return the previous position taking code
		/// page into account. Returns 0 if passed 0.
		/// </summary>
		public int PositionBefore(int pos)
		{
			return (int)SPerform(2417, (uint)pos, 0);
		}	
						
		/// <summary>
		/// Given a valid document position, return the next position taking code
		/// page into account. Maximum value returned is the last position in the document.
		/// </summary>
		public int PositionAfter(int pos)
		{
			return (int)SPerform(2418, (uint)pos, 0);
		}	
						
		/// <summary>
		/// Copy a range of text to the clipboard. Positions are clipped into the document.
		/// </summary>
		public void CopyRange(int start, int end)
		{
			 SPerform(2419, (uint)start, (uint)end);
		}	
						
		/// <summary>
		/// Copy argument text to the clipboard.
		/// </summary>
		unsafe public void CopyText(int length, string text)
		{
			if (text == null || text.Equals(""))text = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(text))
			{
				SPerform(2420,(uint)length, (uint)b);
			}
		}	
						
		/// <summary>
		/// Retrieve the position of the start of the selection at the given line (INVALID_POSITION if no selection on this line).
		/// </summary>
		public int GetLineSelStartPosition(int line)
		{
			return (int)SPerform(2424, (uint)line, 0);
		}	
						
		/// <summary>
		/// Retrieve the position of the end of the selection at the given line (INVALID_POSITION if no selection on this line).
		/// </summary>
		public int GetLineSelEndPosition(int line)
		{
			return (int)SPerform(2425, (uint)line, 0);
		}	
						
		/// <summary>
		/// Move caret down one line, extending rectangular selection to new caret position.
		/// </summary>
		public void LineDownRectExtend()
		{
			SPerform(2426, 0, 0);
		}	
						
		/// <summary>
		/// Move caret up one line, extending rectangular selection to new caret position. 
		/// </summary>
		public void LineUpRectExtend()
		{
			SPerform(2427, 0, 0);
		}	
						
		/// <summary>
		/// Move caret left one character, extending rectangular selection to new caret position.
		/// </summary>
		public void CharLeftRectExtend()
		{
			SPerform(2428, 0, 0);
		}	
						
		/// <summary>
		/// Move caret right one character, extending rectangular selection to new caret position.
		/// </summary>
		public void CharRightRectExtend()
		{
			SPerform(2429, 0, 0);
		}	
						
		/// <summary>
		/// Move caret to first position on line, extending rectangular selection to new caret position.
		/// </summary>
		public void HomeRectExtend()
		{
			SPerform(2430, 0, 0);
		}	
						
		/// <summary>
		/// Move caret to before first visible character on line.
		/// If already there move to first character on line.
		/// In either case, extend rectangular selection to new caret position.
		/// </summary>
		public void VCHomeRectExtend()
		{
			SPerform(2431, 0, 0);
		}	
						
		/// <summary>
		/// Move caret to last position on line, extending rectangular selection to new caret position.
		/// </summary>
		public void LineEndRectExtend()
		{
			SPerform(2432, 0, 0);
		}	
						
		/// <summary>
		/// Move caret one page up, extending rectangular selection to new caret position.
		/// </summary>
		public void PageUpRectExtend()
		{
			SPerform(2433, 0, 0);
		}	
						
		/// <summary>
		/// Move caret one page down, extending rectangular selection to new caret position.
		/// </summary>
		public void PageDownRectExtend()
		{
			SPerform(2434, 0, 0);
		}	
						
		/// <summary>
		/// Move caret to top of page, or one page up if already at top of page.
		/// </summary>
		public void StutteredPageUp()
		{
			SPerform(2435, 0, 0);
		}	
						
		/// <summary>
		/// Move caret to top of page, or one page up if already at top of page, extending selection to new caret position.
		/// </summary>
		public void StutteredPageUpExtend()
		{
			SPerform(2436, 0, 0);
		}	
						
		/// <summary>
		/// Move caret to bottom of page, or one page down if already at bottom of page.
		/// </summary>
		public void StutteredPageDown()
		{
			SPerform(2437, 0, 0);
		}	
						
		/// <summary>
		/// Move caret to bottom of page, or one page down if already at bottom of page, extending selection to new caret position.
		/// </summary>
		public void StutteredPageDownExtend()
		{
			SPerform(2438, 0, 0);
		}	
						
		/// <summary>
		/// Move caret left one word, position cursor at end of word.
		/// </summary>
		public void WordLeftEnd()
		{
			SPerform(2439, 0, 0);
		}	
						
		/// <summary>
		/// Move caret left one word, position cursor at end of word, extending selection to new caret position.
		/// </summary>
		public void WordLeftEndExtend()
		{
			SPerform(2440, 0, 0);
		}	
						
		/// <summary>
		/// Move caret right one word, position cursor at end of word.
		/// </summary>
		public void WordRightEnd()
		{
			SPerform(2441, 0, 0);
		}	
						
		/// <summary>
		/// Move caret right one word, position cursor at end of word, extending selection to new caret position.
		/// </summary>
		public void WordRightEndExtend()
		{
			SPerform(2442, 0, 0);
		}

        /// <summary>
        /// Set the set of characters making up whitespace for when moving or selecting by word. Should be called after WordChars.
        /// </summary>
        unsafe public void WhitespaceChars(string characters)
        {
            if (characters == null || characters.Equals("")) characters = "\0\0";
            fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(characters))
            {
                SPerform(2443, 0, (uint)b);
            }
        }
				
		/// <summary>
		/// Reset the set of characters for whitespace and word characters to the defaults.
		/// </summary>
		public void SetCharsDefault()
		{
			SPerform(2444, 0, 0);
		}
		
		/// <summary>
		/// Enlarge the document to a particular size of text bytes.
		/// </summary>
		public void Allocate(int bytes)
		{
			 SPerform(2446, (uint)bytes, 0);
		}	
						
		/// <summary>
		/// Start notifying the container of all key presses and commands.
		/// </summary>
		public void StartRecord()
		{
			SPerform(3001, 0, 0);
		}	
						
		/// <summary>
		/// Stop notifying the container of all key presses and commands.
		/// </summary>
		public void StopRecord()
		{
			SPerform(3002, 0, 0);
		}	
						
		/// <summary>
		/// Colourise a segment of the document using the current lexing language.
		/// </summary>
		public void Colourise(int start, int end)
		{
			 SPerform(4003, (uint)start, (uint)end);
		}	
						
		/// <summary>
		/// Load a lexer library (dll / so).
		/// </summary>
		unsafe public void LoadLexerLibrary(string path)
		{
			if (path == null || path.Equals("")) path = "\0\0";
			fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(path))
			{
				 SPerform(4007, 0, (uint)b);
			}
		}

        /// <summary>
        /// Find the position of a column on a line taking into account tabs and
        /// multi-byte characters. If beyond end of line, return line end position.
        /// </summary>
        public int FindColumn(int line, int column)
        {
            return (int)SPerform(2456, (uint)line, (uint)column);
        }

        /// <summary>
        /// Turn a indicator on over a range.
        /// </summary>
        public void IndicatorFillRange(int position, int fillLength)
        {
            SPerform(2504, (uint)position, (uint)fillLength);
        }

        /// <summary>
        /// Turn a indicator off over a range.
        /// </summary>
        public void IndicatorClearRange(int position, int clearLength)
        {
            SPerform(2505, (uint)position, (uint)clearLength);
        }

        /// <summary>
        /// Are any indicators present at position?
        /// </summary>
        public int IndicatorAllOnFor(int position)
        {
            return (int)SPerform(2506, (uint)position, 0);
        }

        /// <summary>
        /// What value does a particular indicator have at at a position?
        /// </summary>
        public int IndicatorValueAt(int indicator, int position)
        {
            return (int)SPerform(2507, (uint)indicator, (uint)position);
        }

        /// <summary>
        /// Where does a particular indicator start?
        /// </summary>
        public int IndicatorStart(int indicator, int position)
        {
            return (int)SPerform(2508, (uint)indicator, (uint)position);
        }

        /// <summary>
        /// Where does a particular indicator end?
        /// </summary>
        public int IndicatorEnd(int indicator, int position)
        {
            return (int)SPerform(2509, (uint)indicator, (uint)position);
        }

        /// <summary>
        /// Copy the selection, if selection empty copy the line with the caret
        /// </summary>
        public void CopyAllowLine()
        {
            SPerform(2519, 0, 0);
            // Invoke UI update after copy...
            if (UpdateUI != null) UpdateUI(this);
        }

        /// <summary>
        /// Set the alpha fill colour of the given indicator.
        /// </summary>
        public void SetIndicSetAlpha(int indicator, int alpha)
        {
            SPerform(2523, (uint)indicator, (uint)alpha);
        }

        /// <summary>
        /// Set the alpha fill colour of the given indicator.
        /// </summary>
        public void GetIndicSetAlpha(int indicator)
        {
            SPerform(2524, (uint)indicator, 0);
        }

        /// <summary>
        /// Which symbol was defined for markerNumber with MarkerDefine
        /// </summary>
        public int GetMarkerSymbolDefined(int markerNumber)
        {
            return (int)SPerform(2529, (uint)markerNumber, 0);
        }

        /// <summary>
        /// Set the text in the text margin for a line
        /// </summary>
        public void SetMarginStyle(int line, int style)
        {
            SPerform(2532, (uint)line, (uint)style);
        }

        /// <summary>
        /// Get the style number for the text margin for a line
        /// </summary>
        public int GetMarginStyle(int line)
        {
            return (int)SPerform(2533, (uint)line, 0);
        }

        /// <summary>
        /// Clear the margin text on all lines
        /// </summary>
        public void MarginTextClearAll()
        {
            SPerform(2536, 0, 0);
        }

        /// <summary>
        /// Find the position of a character from a point within the window.
        /// </summary>
        public int GetCharPositionFromPoint(int x, int y)
        {
            return (int)SPerform(2561, (uint)x, (uint)y);
        }

        /// <summary>
        /// Find the position of a character from a point within the window. Return INVALID_POSITION if not close to text.
        /// </summary>
        public int GetCharPositionFromPointClose(int x, int y)
        {
            return (int)SPerform(2562, (uint)x, (uint)y);
        }

        /// <summary>
        /// Add a container action to the undo stack
        /// </summary>
        public void AddUndoAction(int token, int flags)
        {
            SPerform(2560, (uint)token, (uint)flags);
        }

        /// <summary>
        /// Set the style number for the annotations for a line
        /// </summary>
        public void SetAnnotationStyle(int line, int style)
        {
            SPerform(2542, (uint)line, (uint)style);
        }

        /// <summary>
        /// Get the style number for the annotations for a line
        /// </summary>
        public int GetAnnotationStyle(int line)
        {
            return (int)SPerform(2543, (uint)line, 0);
        }

        /// <summary>
        /// Clear the annotations from all lines
        /// </summary>
        public void AnnotatioClearAll()
        {
            SPerform(2547, 0, 0);
        }

        /// <summary>
        /// Get the number of annotation lines for a line
        /// </summary>
        public int GetAnnotationLines(int line)
        {
            return (int)SPerform(2546, (uint)line, 0);
        }

        /// <summary>
        /// Set the text in the text margin for a line
        /// </summary>
        unsafe public void SetMarginText(int line, string text)
        {
            if (text == null || text.Equals("")) text = "\0\0";
            fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(text))
            {
                SPerform(2530, (uint)line, (uint)b);
            }
        }

        /// <summary>
        /// Set the style in the text margin for a line
        /// </summary>
        unsafe public void SetMarginStyles(int line, string styles)
        {
            if (styles == null || styles.Equals("")) styles = "\0\0";
            fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(styles))
            {
                SPerform(2534, (uint)line, (uint)b);
            }
        }

        /// <summary>
        /// Set the annotation text for a line
        /// </summary>
        unsafe public void SetAnnotationText(int line, string text)
        {
            if (text == null || text.Equals("")) text = "\0\0";
            fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(text))
            {
                SPerform(2540, (uint)line, (uint)b);
            }
        }

        /// <summary>
        /// Set the annotation styles for a line
        /// </summary>
        unsafe public void SetAnnotationStyles(int line, string styles)
        {
            if (styles == null || styles.Equals("")) styles = "\0\0";
            fixed (byte* b = Encoding.GetEncoding(this.CodePage).GetBytes(styles))
            {
                SPerform(2544, (uint)line, (uint)b);
            }
        }

        /// <summary>
        /// Get the text in the text margin for a line
        /// </summary>
        unsafe public string GetMarginText(int line)
        {
            int sz = (int)SPerform(2531, (uint)line, 0);
            byte[] buffer = new byte[sz + 1];
            fixed (byte* b = buffer) SPerform(2531, (uint)line + 1, (uint)b);
            return Encoding.GetEncoding(this.CodePage).GetString(buffer, 0, sz - 1);
        }

        /// <summary>
        /// Get the styles in the text margin for a line
        /// </summary>
        unsafe public string GetMarginStyles(int line)
        {
            int sz = (int)SPerform(2535, (uint)line, 0);
            byte[] buffer = new byte[sz + 1];
            fixed (byte* b = buffer) SPerform(2535, (uint)line + 1, (uint)b);
            return Encoding.GetEncoding(this.CodePage).GetString(buffer, 0, sz - 1);
        }

        /// <summary>
        /// Get the annotation text for a line
        /// </summary>
        unsafe public string GetAnnotationText(int line)
        {
            int sz = (int)SPerform(2541, (uint)line, 0);
            byte[] buffer = new byte[sz + 1];
            fixed (byte* b = buffer) SPerform(2541, (uint)line + 1, (uint)b);
            return Encoding.GetEncoding(this.CodePage).GetString(buffer, 0, sz - 1);
        }

        /// <summary>
        /// Get the annotation styles for a line
        /// </summary>
        unsafe public string GetAnnotationStyles(int line)
        {
            int sz = (int)SPerform(2545, (uint)line, 0);
            byte[] buffer = new byte[sz + 1];
            fixed (byte* b = buffer) SPerform(2545, (uint)line + 1, (uint)b);
            return Encoding.GetEncoding(this.CodePage).GetString(buffer, 0, sz - 1);
        }

        /// <summary>
        /// Set caret behavior in virtual space. (1 = allow rectangular selection, 2 = allow cursor movement, 3 = both)
        /// </summary>
        public void SetVirtualSpaceOptions(int options)
        {
            SPerform(2596, (uint)options, 0);
        }

        /// <summary>
        /// Returns caret behavior in virtual space. (1 = allow rectangular selection, 2 = allow cursor movement, 3 = both)
        /// </summary>
        public int GetVirtualSpaceOptions()
        {
            return (int)SPerform(2597, 0, 0);
        }

        /// <summary>
        /// Set whether pasting, typing, backspace, and delete work on all lines of a multiple selection
        /// </summary>
        public void SetMultiSelectionTyping(bool flag)
        {
            uint option = (uint)(flag ? 1 : 0);
            SPerform(2565, option, 0);
            SPerform(2614, option, 0);
        }

        /// <summary>
        /// Returns whether pasting, typing, backspace, and delete work on all lines of a multiple selection
        /// </summary>
        public bool GetMultiSelectionTyping()
        {
            return SPerform(2566, 0, 0) != 0;
        }

        /// <summary>
        /// Find the next line at or after lineStart that is a contracted fold header line.
        /// Return -1 when no more lines.
        /// </summary>
        public int ContractedFoldNext(int lineStart)
        {
            return (int)SPerform(2618, (uint)lineStart, 0);
        }

		#endregion
		
		#region Scintilla Constants

        private const int WM_NOTIFY = 0x004e;
        private const int WM_SYSCHAR = 0x106;
        private const int WM_COMMAND = 0x0111;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_DROPFILES = 0x0233;
		private const uint WS_CHILD = (uint)0x40000000L;
		private const uint WS_VISIBLE = (uint)0x10000000L;
		private const uint WS_TABSTOP = (uint)0x00010000L;
		private const uint WS_CHILD_VISIBLE_TABSTOP = WS_CHILD|WS_VISIBLE|WS_TABSTOP;
        public const int MAXDWELLTIME = 10000000;
		private const int PATH_LEN = 1024;
	
		#endregion
		
        #region Scintilla Shortcuts

        /// <summary>
        /// Initializes the user customizable shortcut overrides
        /// </summary>
        public static void InitShortcuts()
        {
            shortcutOverrides.Add("Scintilla.ResetZoom", Keys.Control | Keys.NumPad0);
            shortcutOverrides.Add("Scintilla.ZoomOut", Keys.Control | Keys.Subtract);
            shortcutOverrides.Add("Scintilla.ZoomIn", Keys.Control | Keys.Add);
            foreach (DictionaryEntry shortcut in shortcutOverrides)
            {
                String id = (String)shortcut.Key;
                Keys keys = (Keys)shortcut.Value;
                PluginBase.MainForm.RegisterShortcutItem(id, keys);
            }
        }

        /// <summary>
        /// Updates the shortcut if it changes or needs updating
        /// </summary>
        public static void UpdateShortcut(String id, Keys shortcut)
        {
            if (id.StartsWith("Scintilla.")) shortcutOverrides[id] = shortcut;
        }

        /// <summary>
        /// Execute the shortcut override using reflection
        /// </summary>
        private Boolean ExecuteShortcut(Int32 keys)
        {
            try
            {
                if (!shortcutOverrides.ContainsValue((Keys)keys)) return false;
                foreach (DictionaryEntry shortcut in shortcutOverrides)
                {
                    if ((Keys)keys == (Keys)shortcut.Value)
                    {
                        String id = shortcut.Key.ToString().Replace("Scintilla.", "");
                        this.GetType().GetMethod(id).Invoke(this, null);
                        return true;
                    }
                }
                return false;
            }
            catch (Exception) { return false; }
        }

        #endregion

        #region Scintilla External

        // Stops all sci events from firing...
        public bool DisableAllSciEvents = false;

		[DllImport("gdi32.dll")] 
		public static extern int GetDeviceCaps(IntPtr hdc, Int32 capindex);
		
		[DllImport("user32.dll")]
		public static extern int SendMessage(int hWnd, uint Msg, int wParam, int lParam);

		[DllImport("user32.dll")]
		public static extern int SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int X, int Y, int cx, int cy, int uFlags);
		
		[DllImport("shell32.dll")]
        public static extern int DragQueryFileA(IntPtr hDrop, uint idx, IntPtr buff, int sz);
                
        [DllImport("shell32.dll")]
        public static extern int DragFinish(IntPtr hDrop);
                
        [DllImport("shell32.dll")]
        public static extern void DragAcceptFiles(IntPtr hwnd, int accept);
        
		[DllImport("scilexer.dll", EntryPoint = "Scintilla_DirectFunction")]
		public static extern int Perform(int directPointer, UInt32 message, UInt32 wParam, UInt32 lParam);

		public UInt32 SlowPerform(UInt32 message, UInt32 wParam, UInt32 lParam)
		{
			return (UInt32)SendMessage((int)hwndScintilla, message, (int)wParam, (int)lParam);
		}

		public UInt32 FastPerform(UInt32 message, UInt32 wParam, UInt32 lParam)
		{
			return (UInt32)Perform(directPointer, message, wParam, lParam);
		}

		public UInt32 SPerform(UInt32 message, UInt32 wParam, UInt32 lParam)
		{
			return (UInt32)Perform(directPointer, message, wParam, lParam);
		}

        public override bool PreProcessMessage(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_KEYDOWN:
                {
                    Int32 keys = (Int32)Control.ModifierKeys + (Int32)m.WParam;
                    if (!IsFocus || ignoreAllKeys || ignoredKeys.ContainsKey(keys))
                    {
                        if (this.ExecuteShortcut(keys) || base.PreProcessMessage(ref m)) return true;
                    }
                    if (((Control.ModifierKeys & Keys.Control) != 0) && ((Control.ModifierKeys & Keys.Alt) == 0))
                    {
                        Int32 code = (Int32)m.WParam;
                        if ((code >= 65) && (code <= 90)) return true; // Eat non-writable characters
                        else if ((code == 9) || (code == 33) || (code == 34)) // Transmit Ctrl with Tab, PageUp/PageDown
                        {
                            return base.PreProcessMessage(ref m);
                        }
                    }
                    break;
                }
                case WM_SYSKEYDOWN:
                {
                    return base.PreProcessMessage(ref m);
                }
                case WM_SYSCHAR:
                {
                    return base.PreProcessMessage(ref m);
                }
            }
            return false;
        }

		protected override void WndProc(ref System.Windows.Forms.Message m)
		{
            if (m.Msg == WM_COMMAND)
            {
                Int32 message = (m.WParam.ToInt32() >> 16) & 0xffff;
                if (message == (int)Enums.Command.SetFocus || message == (int)Enums.Command.KillFocus)
                {
                    if (FocusChanged != null) FocusChanged(this);
                }
            }
            else if (m.Msg == WM_NOTIFY)
			{
				SCNotification scn = (SCNotification)Marshal.PtrToStructure(m.LParam, typeof(SCNotification));
                if (scn.nmhdr.hwndFrom == hwndScintilla && !this.DisableAllSciEvents) 
				{
					switch (scn.nmhdr.code)
					{
						case (uint)Enums.ScintillaEvents.StyleNeeded:
							if (StyleNeeded != null) StyleNeeded(this, scn.position);
							break;
							
						case (uint)Enums.ScintillaEvents.CharAdded:
							if (CharAdded != null) CharAdded(this, scn.ch);
							break;
							
						case (uint)Enums.ScintillaEvents.SavePointReached:
							if (SavePointReached != null) SavePointReached(this);
							break;
							
						case (uint)Enums.ScintillaEvents.SavePointLeft:
							if (SavePointLeft != null) SavePointLeft(this);
							break;
							
						case (uint)Enums.ScintillaEvents.ModifyAttemptRO:
							if (ModifyAttemptRO != null) ModifyAttemptRO(this);
							break;

						case (uint)Enums.ScintillaEvents.Key:
							if (Key != null) Key(this, scn.ch, scn.modifiers);
							break;

						case (uint)Enums.ScintillaEvents.DoubleClick:
							if (DoubleClick != null) DoubleClick(this);
							break;

						case (uint)Enums.ScintillaEvents.UpdateUI:
							if (UpdateUI != null) UpdateUI(this);
							break;

						case (uint)Enums.ScintillaEvents.MacroRecord:
							if (MacroRecord != null) MacroRecord(this, scn.message, scn.wParam, scn.lParam);
							break;

						case (uint)Enums.ScintillaEvents.MarginClick:
							if (MarginClick != null) MarginClick(this, scn.modifiers, scn.position, scn.margin);
							break;

						case (uint)Enums.ScintillaEvents.NeedShown:
							if (NeedShown != null) NeedShown(this, scn.position, scn.length);
							break;

						case (uint)Enums.ScintillaEvents.Painted:
							if (Painted != null) Painted(this);
							break;

						case (uint)Enums.ScintillaEvents.UserListSelection:
							if (UserListSelection != null) UserListSelection(this, scn.listType, MarshalStr(scn.text));
							break;

						case (uint)Enums.ScintillaEvents.URIDropped:
							if (URIDropped != null) URIDropped(this, MarshalStr(scn.text));
							break;

						case (uint)Enums.ScintillaEvents.DwellStart:
							if (DwellStart != null) DwellStart(this, scn.position);
							break;

						case (uint)Enums.ScintillaEvents.DwellEnd:
							if (DwellEnd != null) DwellEnd(this, scn.position);
							break;

						case (uint)Enums.ScintillaEvents.Zoom:
							if (Zoom != null) Zoom(this);
							break;

						case (uint)Enums.ScintillaEvents.HotspotClick:
							if (HotSpotClick != null) HotSpotClick(this, scn.modifiers, scn.position);
							break;

						case (uint)Enums.ScintillaEvents.HotspotDoubleClick:
							if (HotSpotDoubleClick != null) HotSpotDoubleClick(this, scn.modifiers, scn.position);
							break;

						case (uint)Enums.ScintillaEvents.CalltipClick:
							if (CallTipClick != null) CallTipClick(this, scn.position);
							break;

						case (uint)Enums.ScintillaEvents.AutoCSelection:
							if (AutoCSelection != null) AutoCSelection(this, MarshalStr(scn.text));
							break;

                        case (uint)Enums.ScintillaEvents.IndicatorClick:
                            if (IndicatorClick != null) IndicatorClick(this, scn.position);
                            break;

                        case (uint)Enums.ScintillaEvents.IndicatorRelease:
                            if (IndicatorRelease != null) IndicatorRelease(this, scn.position);
                            break;

                        case (uint)Enums.ScintillaEvents.AutoCCharDeleted:
                            if (AutoCCharDeleted != null) AutoCCharDeleted(this);
                            break;

                        case (uint)Enums.ScintillaEvents.AutoCCancelled:
                            if (AutoCCancelled != null) AutoCCancelled(this);
                            break;

						case (uint)Enums.ScintillaEvents.Modified:
                            bool notify = false;
							if ((scn.modificationType & (uint)Enums.ModificationFlags.InsertText)>0)
							{
								if (TextInserted != null) TextInserted(this, scn.position, scn.length, scn.linesAdded);
                                notify = true;
							}
							if ((scn.modificationType & (uint)Enums.ModificationFlags.DeleteText)>0) 
							{
								if (TextDeleted != null) TextDeleted(this, scn.position, scn.length, scn.linesAdded);
                                notify = true;
							}
							if ((scn.modificationType & (uint)Enums.ModificationFlags.ChangeStyle)>0) 
							{
								if (StyleChanged != null) StyleChanged(this, scn.position, scn.length);
							}
							if ((scn.modificationType & (uint)Enums.ModificationFlags.ChangeFold)>0)
							{
								if (FoldChanged != null ) FoldChanged(this, scn.line, scn.foldLevelNow, scn.foldLevelPrev);
							}
							if ((scn.modificationType & (uint)Enums.ModificationFlags.UserPerformed)>0) 
							{
								if (UserPerformed != null ) UserPerformed(this);
							}
							if ((scn.modificationType & (uint)Enums.ModificationFlags.UndoPerformed)>0)
							{
								if (UndoPerformed != null ) UndoPerformed(this);
                                notify = true;
							}
							if ((scn.modificationType & (uint)Enums.ModificationFlags.RedoPerformed)>0)
							{
								if (RedoPerformed != null ) RedoPerformed(this);
                                notify = true;
							}
							if ((scn.modificationType & (uint)Enums.ModificationFlags.LastStepInUndoRedo)>0)
							{
								if (LastStepInUndoRedo != null ) LastStepInUndoRedo(this);
							}
							if ((scn.modificationType & (uint)Enums.ModificationFlags.ChangeMarker)>0)
							{
								if (MarkerChanged != null ) MarkerChanged(this, scn.line);
							}
							if ((scn.modificationType & (uint)Enums.ModificationFlags.BeforeInsert)>0)
							{
								if (BeforeInsert != null ) BeforeInsert(this, scn.position, scn.length);
                                notify = false;
							}
							if ((scn.modificationType & (uint)Enums.ModificationFlags.BeforeDelete)>0)
							{
								if (BeforeDelete != null ) BeforeDelete(this, scn.position, scn.length);
                                notify = false;
							}
                            if (notify && Modified != null && scn.text != null)
							{
                                try
                                {
                                    string text = MarshalStr(scn.text, scn.length);
                                    Modified(this, scn.position, scn.modificationType, text, scn.length, scn.linesAdded, scn.line, scn.foldLevelNow, scn.foldLevelPrev);
                                }
                                catch {}
							}
							break;
					}
				}
			}
			else if (m.Msg == WM_DROPFILES)
			{
				HandleFileDrop(m.WParam);
			}
			else
			{
				base.WndProc(ref m);
			}
		}
		
		unsafe string MarshalStr(IntPtr p) 
		{
		   sbyte* b = (sbyte*)p;
		   int len = 0;
		   while (b[len] != 0) ++len;
		   return new string(b,0,len);
		}
		
		unsafe string MarshalStr(IntPtr p, int len) 
		{
		   sbyte* b = (sbyte*)p;
		   return new string(b,0,len);
		}
		
		#endregion
		
		#region Automated Features

        /// <summary>
        /// Provides basic highlighting of selected text
        /// </summary>
        private void OnSelectHighlight(ScintillaControl sci)
        {
            sci.RemoveHighlights();
            if (Control.ModifierKeys == Keys.Control && sci.SelText.Length != 0)
            {
                Language language = Configuration.GetLanguage(sci.ConfigurationLanguage);
                Int32 color = language.editorstyle.HighlightBackColor;
                String pattern = sci.SelText.Trim();
                FRSearch search = new FRSearch(pattern);
                search.WholeWord = true; search.NoCase = false;
                search.Filter = SearchFilter.None; // Everywhere
                sci.AddHighlights(search.Matches(sci.Text), color);
                sci.hasHighlights = true;
            }
        }
        private void OnCancelHighlight(ScintillaControl sci)
        {
            if (sci.isHiliteSelected && sci.hasHighlights && sci.SelText.Length == 0)
            {
                sci.RemoveHighlights();
                sci.hasHighlights = false;
            }
        }

        /// <summary>
        /// Provides the support for code block selection
        /// </summary>
        private void OnBlockSelect(ScintillaControl sci)
        {
            int position = CurrentPos - 1;
            char character = (char)CharAt(position);
            if (character == '{' || character == '(' || character == '[')
            {
                if (!this.PositionIsOnComment(position))
                {
                    int bracePosStart = position;
                    int bracePosEnd = BraceMatch(position);
                    if (bracePosEnd != -1) SetSel(bracePosStart, bracePosEnd + 1);
                }
            }
        }

		/// <summary>
		/// Provides the support for brace matching
		/// </summary>
		private void OnBraceMatch(ScintillaControl sci)
		{
			if (isBraceMatching && sci.SelText.Length == 0)
			{
				int position = CurrentPos-1;
				char character = (char)CharAt(position);
				if (character != '{' && character != '}' && character != '(' && character != ')' && character != '[' && character != ']')
				{
					position = CurrentPos;
					character = (char)CharAt(position);
				}
                if (character == '{' || character == '}' || character == '(' || character == ')' || character == '[' || character == ']')
                {
                    if (!this.PositionIsOnComment(position))
                    {
                        int bracePosStart = position;
                        int bracePosEnd = BraceMatch(position);
                        if (bracePosEnd != -1) BraceHighlight(bracePosStart, bracePosEnd);
                        if (useHighlightGuides)
                        {
                            int line = LineFromPosition(position);
                            HighlightGuide = GetLineIndentation(line);
                        }
                    }
                    else
                    {
                        BraceHighlight(-1, -1);
                        HighlightGuide = 0;
                    }
                }
                else
                {
                    BraceHighlight(-1, -1);
                    HighlightGuide = 0;
                }
			}
		}
		
		/// <summary>
		/// Provides support for smart indenting
		/// </summary>
        ///
        private void OnSmartIndent(ScintillaControl ctrl, int ch)
		{
            char newline = (EOLMode == 1) ? '\r' : '\n';
			switch (SmartIndentType)
			{
				case Enums.SmartIndent.None:
					return;
				case Enums.SmartIndent.Simple:
					if (ch == newline)
					{
                        this.BeginUndoAction();
                        try
                        {
                            int curLine = LineFromPosition(CurrentPos);
                            int previousIndent = GetLineIndentation(curLine - 1);
                            IndentLine(curLine, previousIndent);
                            int position = LineIndentPosition(curLine);
                            SetSel(position, position);
                        }
                        finally
                        {
                            this.EndUndoAction();
                        }
					}
					break;
				case Enums.SmartIndent.CPP:
					if (ch == newline)
					{
                        this.BeginUndoAction();
                        try
                        {
                            int curLine = LineFromPosition(CurrentPos);
                            int tempLine = curLine;
                            int previousIndent;
                            string tempText;
                            do
                            {
                                --tempLine;
                                previousIndent = GetLineIndentation(tempLine);
                                tempText = GetLine(tempLine).TrimEnd();
                                if (tempText.Length == 0) previousIndent = -1;
                            }
                            while ((tempLine > 0) && (previousIndent < 0));
                            if (tempText.IndexOf("//") > 0) // remove comment at end of line
                            {
                                int slashes = this.MBSafeTextLength(tempText.Substring(0, tempText.IndexOf("//") + 1));
                                if (this.PositionIsOnComment(PositionFromLine(tempLine) + slashes))
                                    tempText = tempText.Substring(0, tempText.IndexOf("//")).Trim();
                            }
                            if (tempText.EndsWith("{"))
                            {
                                int bracePos = CurrentPos - 1;
                                while (bracePos > 0 && CharAt(bracePos) != '{') bracePos--;
                                int style = BaseStyleAt(bracePos);
                                if (bracePos >= 0 && CharAt(bracePos) == '{' && (style == 10/*CPP*/ || style == 5/*CSS*/)) 
                                    previousIndent += TabWidth;
                            }
                            // TODO: Should this test a config variable for indenting after case : statements?
                            if (Lexer == 3 && tempText.EndsWith(":") && !tempText.EndsWith("::"))
                            {
                                int prevLine = tempLine;
                                while (--prevLine > 0)
                                {
                                    tempText = GetLine(prevLine).Trim();
                                    if (tempText.Length != 0 && !tempText.StartsWith("//"))
                                    {
                                        int prevIndent = GetLineIndentation(prevLine);
                                        if ((tempText.EndsWith(";") && previousIndent == prevIndent) ||
                                            (tempText.EndsWith(":") && previousIndent == prevIndent + Indent))
                                        {
                                            previousIndent -= Indent;
                                            SetLineIndentation(tempLine, previousIndent);
                                        }
                                        break;
                                    }
                                }
                                previousIndent += Indent;
                            }
                            IndentLine(curLine, previousIndent);
                            int position = LineIndentPosition(curLine);
                            SetSel(position, position);
                            if (Lexer == 3 && Control.ModifierKeys == Keys.Shift)
                            {
                                int endPos = LineEndPosition(curLine - 1);
                                int style = BaseStyleAt(endPos - 1);
                                if (style == 12 || style == 7)
                                {
                                    string quote = GetStringType(endPos).ToString();
                                    InsertText(endPos, quote);
                                    InsertText(position + 1, "+ " + quote);
                                    GotoPos(position + 4);
                                    //if (Regex.IsMatch(GetLine(curLine - 1), "=[\\s]*" + quote))
                                        SetLineIndentation(curLine, GetLineIndentation(curLine - 1) + TabWidth);
                                }
                            }
                        }
                        finally
                        {
                            this.EndUndoAction();
                        }
					}
					else if (ch == '}')
					{
                        this.BeginUndoAction();
                        try
                        {
                            int position = CurrentPos;
                            int curLine = LineFromPosition(position);
                            int previousIndent = GetLineIndentation(curLine - 1);
                            int match = SafeBraceMatch(position - 1);
                            if (match != -1)
                            {
                                previousIndent = GetLineIndentation(LineFromPosition(match));
                                IndentLine(curLine, previousIndent);
                            }
                        }
                        finally
                        {
                            this.EndUndoAction();
                        }
					}
					break;
				case Enums.SmartIndent.Custom:
					if (ch == newline)
					{
						if (SmartIndent != null) SmartIndent(this);
					}
					break;
			}
		}

        /// <summary>
        /// Detects the string-literal quote style
        /// </summary>
        /// <param name="position">lookup position</param>
        /// <returns>' or " or Space if undefined</returns>
        public char GetStringType(int position)
        {
            char next = (char)CharAt(position);
            char c;
            for (int i = position; i > 0; i--)
            {
                c = next;
                next = (char)CharAt(i-1);

                if (next == '\\' && (c == '\'' || c == '"')) i--;
                if (c == '\'') return '\'';
                else if (c == '"') return '"';
            }
            return ' ';
        }
		
		#endregion 

        #region Misc Custom Stuff

		/// <summary>
		/// Render the contents for printing
		/// </summary>
		public int FormatRange(bool measureOnly, PrintPageEventArgs e, int charFrom, int charTo)
		{
			IntPtr hdc = e.Graphics.GetHdc();
			int wParam = (measureOnly ? 0 : 1);
			RangeToFormat frPrint = this.GetRangeToFormat(hdc, charFrom, charTo);
			IntPtr lParam = Marshal.AllocCoTaskMem(Marshal.SizeOf(frPrint));
			Marshal.StructureToPtr(frPrint, lParam, false);
			int res = (int)this.SPerform(2151, (uint)wParam, (uint)lParam);
			Marshal.FreeCoTaskMem(lParam);
			e.Graphics.ReleaseHdc(hdc);
			return res;
		}
		
		/// <summary>
		/// Populates the RangeToFormat struct
		/// </summary>
		private RangeToFormat GetRangeToFormat(IntPtr hdc, int charFrom, int charTo)
		{
			RangeToFormat frPrint;
			int pageWidth = (int)GetDeviceCaps(hdc, 110);
			int pageHeight = (int)GetDeviceCaps(hdc, 111);
			frPrint.hdcTarget = hdc;
			frPrint.hdc = hdc;
			frPrint.rcPage.Left = 0;
			frPrint.rcPage.Top = 0;
			frPrint.rcPage.Right = pageWidth;
			frPrint.rcPage.Bottom = pageHeight;
			frPrint.rc.Left = Convert.ToInt32(pageWidth*0.02);
			frPrint.rc.Top = Convert.ToInt32(pageHeight*0.03);
			frPrint.rc.Right = Convert.ToInt32(pageWidth*0.975);
			frPrint.rc.Bottom = Convert.ToInt32(pageHeight*0.95);
			frPrint.chrg.cpMin = charFrom;
			frPrint.chrg.cpMax = charTo;
			return frPrint;
		}
		
		/// <summary>
		/// Free cached data from the control after printing
		/// </summary>
		public void FormatRangeDone()
		{
		    this.SPerform(2151, 0, 0);
		}
		
		/// <summary>
		/// This holds the actual encoding of the document
		/// </summary>
		public Encoding Encoding
		{
			get { return this.encoding; }
            set
            {
                this.encoding = value;
                if (UpdateSync != null) this.UpdateSync(this);
            }
        }

        /// <summary>
        /// Indicate that BOM characters should be written when saving
        /// </summary>
        public bool SaveBOM
        {
            get { return this.saveBOM; }
            set 
            { 
                this.saveBOM = value;
                if (UpdateSync != null) this.UpdateSync(this);
            }
        }
		
		/// <summary>
		/// Adds a line end marker to the end of the document
		/// </summary>
		public void AddLastLineEnd()
		{
			string eolMarker = "\r\n";
			if (this.EOLMode == 1) eolMarker = "\r";
			else if (this.EOLMode == 2) eolMarker = "\n";
			if (!this.Text.EndsWith(eolMarker))
			{
				this.TargetStart = this.TargetEnd = this.TextLength;
				this.ReplaceTarget(eolMarker.Length, eolMarker);
			}
		}
		
		/// <summary>
		/// Removes trailing spaces from each line
		/// </summary>
        public void StripTrailingSpaces()
        {
            this.StripTrailingSpaces(false);
        }
		public void StripTrailingSpaces(Boolean keepIndentTabs)
		{
            this.BeginUndoAction();
            try
            {
                int maxLines = this.LineCount;
                for (int line = 0; line < maxLines; line++)
                {
                    int lineStart = this.PositionFromLine(line);
                    int lineEnd = this.LineEndPosition(line);
                    int i = lineEnd - 1;
                    char ch = (char)this.CharAt(i);
                    while ((i >= lineStart) && ((ch == ' ') || (ch == '\t')))
                    {
                        i--;
                        ch = (char)this.CharAt(i);
                    }
                    if (keepIndentTabs && i == lineStart - 1)
                    {
                        ch = (char)this.CharAt(i + 1);
                        while (i < lineEnd && ch == '\t')
                        {
                            i++;
                            ch = (char)this.CharAt(i + 1);
                        }
                    }
                    if (i < (lineEnd - 1))
                    {
                        this.TargetStart = i + 1;
                        this.TargetEnd = lineEnd;
                        this.ReplaceTarget(0, "");
                    }
                }
            }
            finally
            {
                this.EndUndoAction();
            }
		}

        /// <summary>
        /// Checks if a line is in preprocessor block
        /// </summary>
        public bool LineIsInPreprocessor(ScintillaControl sci, int lexerPpStyle, int line)
        {
            bool ppEnd = false;
            bool ppStart = false;
            int foldHeader = (int)ScintillaNet.Enums.FoldLevel.HeaderFlag;
            for (var i = line; i > 0; i--)
            {
                int pos = sci.PositionFromLine(i);
                int ind = sci.GetLineIndentation(i);
                int style = sci.BaseStyleAt(pos + ind);
                if (style == lexerPpStyle)
                {
                    int fold = sci.GetFoldLevel(i) & foldHeader;
                    if (fold == foldHeader) ppStart = true;
                    break;
                }
            }
            for (var i = line; i < sci.LineCount; i++)
            {
                int pos = sci.PositionFromLine(i);
                int ind = sci.GetLineIndentation(i);
                int style = sci.BaseStyleAt(pos + ind);
                if (style == lexerPpStyle)
                {
                    int fold = sci.GetFoldLevel(i) & foldHeader;
                    if (fold != foldHeader) ppEnd = true;
                    break;
                }
            }
            if (ppStart && ppEnd) return true;
            else return false;
        }

		/// <summary>
		/// Checks that if the specified position is on comment.
        /// NOTE: You may need to manually update coloring: "sci.Colourise(0, -1);"
		/// </summary>
		public bool PositionIsOnComment(int position)
		{
			return PositionIsOnComment(position, this.Lexer);
		}
		public bool PositionIsOnComment(int position, int lexer)
		{
			int style = BaseStyleAt(position);
			if (lexer == 3 || lexer == 18 || lexer == 25 || lexer == 27)
            {
                return (    // cpp, tcl, bullant or pascal
			    style == 1
			    || style == 2 
			    || style == 3
			    || style == 15
				|| style == 17
				|| style == 18);
			}
			else if (lexer == 4 || lexer == 5)
            {
                return (    // html or xml
		        style == 9
		        || style == 20 
		        || style == 29 
		        || style == 30 
		        || style == 42 
		        || style == 43 
		        || style == 44 
		        || style == 57 
		        || style == 58 
		        || style == 59
		        || style == 72
		        || style == 82
		        || style == 92
		        || style == 107
		        || style == 124
		        || style == 125);
			}
			else if (lexer == 2 || lexer == 21)
            {
                return (    // python or lisp
			    style == 1
			    || style == 12);
			}
			else if (lexer == 6 || lexer == 22 || lexer == 45 || lexer == 62)
            {
                return (    // perl, bash, clarion/clw or ruby
			    style == 2);
			}
			else if (lexer == 7)
            {
                return (    // sql
			    style == 1
				|| style == 2 
				|| style == 3
				|| style == 13
				|| style == 15
				|| style == 17
				|| style == 18);
			}
			else if (lexer == 8 || lexer == 9 || lexer == 11 || lexer == 12 || lexer == 16 || lexer == 17 || lexer == 19 || lexer == 23 || lexer == 24 || lexer == 26 || lexer == 28 || lexer == 32 || lexer == 36 || lexer == 37 || lexer == 40 || lexer == 44 || lexer == 48 || lexer == 51 || lexer == 53 || lexer == 54 || lexer == 57 || lexer == 63)
            {
                return (    // asn1, vb, diff, batch, makefile, avenue, eiffel, eiffelkw, vbscript, matlab, crontab, fortran, f77, lout, mmixal, yaml, powerbasic, erlang, octave, kix or properties
			    style == 1);
			}
			else if (lexer == 14)
            {
                return (    // latex
			    style == 4);
			}
			else if (lexer == 15 || lexer == 41 || lexer == 56)
            {
                return (    // lua, verilog or escript
			    style == 1
			   	|| style == 2
			  	|| style == 3);
			}
			else if (lexer == 20)
            {
                return (    // ada
			    style == 10);
			}
			else if (lexer == 31 || lexer == 39 || lexer == 42 || lexer == 52 || lexer == 55 || lexer == 58 || lexer == 60 || lexer == 61 || lexer == 64 || lexer == 71)
            {
                return (    // au3, apdl, baan, ps, mssql, rebol, forth, gui4cli, vhdl or pov
			    style == 1
				|| style == 2);
			}
			else if (lexer == 34)
            {
                return (    // asm
			    style == 1
				|| style == 11);
			}
			else if (lexer == 43)
            {
                return (    // nsis
			    style == 1
				|| style == 18);
			}
			else if (lexer == 59)
            {
                return (    // specman
			    style == 2
				|| style == 3);
			}
			else if (lexer == 70)
            {
                return (    // tads3
                style == 3
                || style == 4);
			}
			else if (lexer == 74)
            {
                return (    // csound
			    style == 1
				|| style == 9);
			}
			else if (lexer == 65)
            {
                return (    // caml
			    style == 12
				|| style == 13
				|| style == 14
				|| style == 15);
			}
			else if (lexer == 68)
            {
                return (    // haskell
			    style == 13
				|| style == 14
				|| style == 15
				|| style == 16);
			}
			else if (lexer == 73)
            {
                return (    // flagship
			    style == 1
				|| style == 2
				|| style == 3
				|| style == 4
				|| style == 5
				|| style == 6);
			}
			else if (lexer == 72) 
            {
                return (    // smalltalk
			    style == 3);
			}
			else if (lexer == 38) 
            {
                return (    // css
			    style == 9);
			}
			return false;
		}
		
		/// <summary>
		/// Indents the specified line
		/// </summary>
		protected void IndentLine(int line, int indent)
		{
			if (indent < 0) return;
			int selStart = SelectionStart;
			int selEnd = SelectionEnd;
			int posBefore = LineIndentPosition(line);
			SetLineIndentation(line, indent);
			int posAfter = LineIndentPosition(line);
			int posDifference = posAfter - posBefore;
			if (posAfter > posBefore)
			{
				if (selStart >= posBefore) selStart += posDifference;
				if (selEnd >= posBefore) selEnd += posDifference;
			}
			else if (posAfter < posBefore)
			{
				if (selStart >= posAfter)
				{
					if (selStart >= posBefore) selStart += posDifference;
					else selStart = posAfter;
				}
				if (selEnd >= posAfter)
				{
					if (selEnd >= posBefore) selEnd += posDifference;
					else selEnd = posAfter;
				}
			}
			SetSel(selStart, selEnd);
		}
		
		/// <summary>
		/// Expands all folds
		/// </summary>
		public void ExpandAllFolds()
		{
			for (int i = 0; i<LineCount; i++)
			{
				FoldExpanded(i, true);
				ShowLines(i+1, i+1);
			}
		}
		
		/// <summary>
		/// Collapses all folds
		/// </summary>
		public void CollapseAllFolds()
		{
			for (int i = 0; i<LineCount; i++)
			{
				int maxSubOrd = LastChild(i, -1);
				FoldExpanded(i, false);
				HideLines(i+1, maxSubOrd);
			}
        }

        /// <summary>
        /// Only folds functions keeping the blocks within it open
        /// </summary>
        public void CollapseFunctions()
        {
            int lineCount = LineCount;

            for (int i = 0; i < lineCount; i++)
            {
                // Determine if function block
                string line = GetLine(i);
                if (line.Contains("function"))
                {
                    // Find the line with the closing ) of the function header
                    while (!line.Contains(")") && i < lineCount)
                    {
                        i++;
                        line = GetLine(i);
                    }

                    // Get the function closing brace
                    int maxSubOrd = LastChild(i, -1);
                    // Get brace if on the next line
                    if (maxSubOrd == i)
                    {
                        i++;
                        maxSubOrd = LastChild(i, -1);
                    }
                    FoldExpanded(i, false);
                    HideLines(i + 1, maxSubOrd);
                    i = maxSubOrd;
                }
                else
                {
                    FoldExpanded(i, true);
                    ShowLines(i + 1, i + 1);
                }
            }
        }

        /// <summary>
        /// Only folds regions and functions keeping the blocks within it open
        /// </summary>
        public void CollapseRegions()
        {
            // hide all lines inside some blocks, show lines outside
            for (int i = 0; i < LineCount; i++)
            {
                // if region/function block
                string line = GetLine(i);
                if (line.Contains("//{") || (line.Contains("function") && line.Contains("(") && line.Contains(")")))
                {
                    // Get the function closing brace
                    int maxSubOrd = LastChild(i, -1);
                    // Get brace if on the next line
                    if (maxSubOrd == i)
                    {
                        i++;
                        maxSubOrd = LastChild(i, -1);
                    }
                    // hide all lines inside
                    HideLines(i + 1, maxSubOrd);
                    i = maxSubOrd;
                }
                else
                {
                    // show lines outside
                    ShowLines(i + 1, i + 1);
                }
            }
            // collapse some block lines, expand all other type of lines
            for (int i = 0; i < LineCount; i++)
            {
                // if region/function block
                string line = GetLine(i);
                if (line.Contains("//{") || (line.Contains("function") && line.Contains("(") && line.Contains(")")))
                {
                    // Get the function closing brace
                    int maxSubOrd = LastChild(i, -1);
                    // Get brace if on the next line
                    if (maxSubOrd == i)
                    {
                        i++;
                        maxSubOrd = LastChild(i, -1);
                    }
                    // collapse some block lines
                    FoldExpanded(i, false);
                }
                else
                {
                    // expand all other type of lines
                    FoldExpanded(i, true);
                }
            }
        }

		/// <summary>
		/// Selects the specified text, starting from the caret position
		/// </summary>
		public int SelectText(string text)
		{
            int pos = this.Text.IndexOf(text, MBSafeCharPosition(this.CurrentPos));
            if (pos >= 0) this.MBSafeSetSel(pos, text);
            return pos;
		}

        /// <summary>
        /// Selects the specified text, starting from the given position
        /// </summary>
        public int SelectText(string text, int startPos)
        {
            int pos = this.Text.IndexOf(text, startPos);
            if (pos >= 0) this.MBSafeSetSel(pos, text);
            return pos;
        }

		/// <summary>
		/// Gets a word from the specified position
		/// </summary>
		public string GetWordFromPosition(int position)
		{
			try
			{
				int startPosition = this.MBSafeCharPosition(this.WordStartPosition(position, true));
				int endPosition = this.MBSafeCharPosition(this.WordEndPosition(position, true));
				string keyword = this.Text.Substring(startPosition, endPosition - startPosition);
				if (keyword == "" || keyword == " ") return null;
				return keyword.Trim();
			}
			catch
			{
				return null;
			}
		}
		
        /// <summary>
        /// Insert text with wide-char to byte position conversion
        /// </summary>
		public void MBSafeInsertText(int position, string text)
		{
			if (this.CodePage != 65001)
			{
				this.InsertText(position, text);
			}
			else
			{	
				int mbpos = this.MBSafePosition(position);
				this.InsertText(mbpos, text);
			}
		}

        /// <summary>
        /// Set cursor position with wide-char to byte position conversion
        /// </summary>
		public void MBSafeGotoPos(int position)
		{
			if (this.CodePage != 65001)
			{
				this.GotoPos(position);
			}
			else
			{
				int mbpos = this.MBSafePosition(position);
				this.GotoPos(mbpos);
			}
		}

        /// <summary>
        /// Select text using wide-char to byte indexes conversion
        /// </summary>
		public void MBSafeSetSel(int start, int end)
		{
			if (this.CodePage != 65001)
			{
				this.SetSel(start, end);
			}
			else
			{
				string count = this.Text.Substring(start, end-start);
				start = this.MBSafePosition(start);
				end = start+this.MBSafeTextLength(count);
				this.SetSel(start, end);
			}
		}

        /// <summary>
        /// Select text using wide-char to byte index & text conversion
        /// </summary>
		public void MBSafeSetSel(int start, string text)
		{
			if (this.CodePage != 65001)
			{
				this.SetSel(start, start+text.Length);
			}
			else
			{
				int mbpos = this.MBSafePosition(start);
				this.SetSel(mbpos, mbpos+this.MBSafeTextLength(text));
			}
		}

        /// <summary>
        /// Wide-char to byte position in the editor text
        /// </summary>
		public int MBSafePosition(int position)
		{
            if (this.CodePage != 65001)
            {
                return position;
            }
            else if (position < 0) return position;
            else
			{
				string count = this.Text.Substring(0, position);
				int mbpos = Encoding.UTF8.GetByteCount(count);
				return mbpos;
			}
		}

        /// <summary>
        /// Byte to wide-char position in the editor text
        /// </summary>
		public int MBSafeCharPosition(int bytePosition)
		{
			if (this.CodePage != 65001)
			{
				return bytePosition;
			}
            else if (bytePosition < 0) return bytePosition;
            else
			{
				byte[] bytes = Encoding.UTF8.GetBytes(this.Text);
				int chrpos = Encoding.UTF8.GetCharCount(bytes, 0, bytePosition);
				return chrpos;
			}
		}

        /// <summary>
        /// Counts byte length of wide-char text
        /// </summary>
		public int MBSafeTextLength(string text)
		{
			if (this.CodePage != 65001)
			{
				return text.Length;
			}
			else
			{
				int mblength = Encoding.UTF8.GetByteCount(text);
                return mblength;
			}
        }

        /// <summary>
        /// Converts bytes count to text length
        /// </summary>
        /// <param name="txt">Reference text</param>
        /// <param name="bytes">Bytes count</param>
        /// <returns>Multi-byte chars length</returns>
        public int MBSafeLengthFromBytes(string txt, int bytes)
        {
            if (this.CodePage != 65001) return bytes;
            byte[] raw = Encoding.UTF8.GetBytes(txt);
            return Encoding.UTF8.GetString(raw, 0, Math.Min(raw.Length, bytes)).Length;
        }
		
		/// <summary>
		/// Custom way to find the matching brace when BraceMatch() does not work
		/// </summary>
		public int SafeBraceMatch(int position)
		{
			int match = this.CharAt(position);
			int toMatch = 0;
			int length = TextLength;
			int ch;
			int sub = 0;
			int lexer = Lexer;
			Colourise(0, -1);
			bool comment = PositionIsOnComment(position, lexer);
			switch (match)
			{
				case '{':
					toMatch = '}';
					goto down;
				case '(':
					toMatch = ')';
					goto down;
				case '[':
					toMatch = ']';
					goto down;
				case '}':
					toMatch = '{';
					goto up;
				case ')':
					toMatch = '(';
					goto up;
				case ']':
					toMatch = '[';
					goto up;
			}
			return -1;
			// search up
			up:
			while (position >= 0)
			{
				position--;
				ch = CharAt(position);
				if (ch == match) 
				{
					if (comment == PositionIsOnComment(position, lexer)) sub++;
				}
				else if (ch == toMatch && comment == PositionIsOnComment(position, lexer))
				{
					sub--;
					if (sub < 0) return position;
				}
			}
			return -1;
			// search down
			down:
			while (position < length)
			{
				position++;
				ch = CharAt(position);
				if (ch == match) 
				{
					if (comment == PositionIsOnComment(position, lexer)) sub++;
				}
				else if (ch == toMatch && comment == PositionIsOnComment(position, lexer)) 
				{
					sub--;
					if (sub < 0) return position;
				}
			}
			return -1;
		}
		
		/// <summary>
		/// File dropped on the control, fire URIDropped event
		/// </summary>
		unsafe void HandleFileDrop(IntPtr hDrop) 
		{
			int nfiles = DragQueryFileA(hDrop, 0xffffffff, (IntPtr)null, 0);
			string files = "";
			byte[] buffer = new byte[PATH_LEN];
			for (uint i = 0; i<nfiles; i++) 
			{
				fixed (byte* b = buffer) 
				{
					DragQueryFileA(hDrop, i, (IntPtr)b, PATH_LEN);
					if (files.Length > 0) files += ' ';
					files += '"'+MarshalStr((IntPtr)b) + '"';
				}
			}
			DragFinish(hDrop);
			if (URIDropped != null) URIDropped(this, files);                        
		}
		
		
		/// <summary>
		/// Returns the base style (without indicators) byte at the position.
		/// </summary>
		public int BaseStyleAt(int pos)
		{
			return (int)(SPerform(2010, (uint)pos, 0) & ((1 << this.StyleBits) - 1));
		}

        /// <summary>
        /// Adds the specified highlights to the control
        /// </summary>
        public void AddHighlights(List<SearchMatch> matches, Int32 highlightColor)
        {
            ITabbedDocument doc = DocumentManager.FindDocument(this);
            if (matches == null || doc == null) return;
            foreach (SearchMatch match in matches)
            {
                Int32 start = this.MBSafePosition(match.Index);
                Int32 end = start + this.MBSafeTextLength(match.Value);
                Int32 line = this.LineFromPosition(start);
                Int32 position = start; Int32 mask = 1 << this.StyleBits;
                // Define indics in both controls...
                doc.SplitSci1.SetIndicStyle(0, (Int32)ScintillaNet.Enums.IndicatorStyle.RoundBox);
                doc.SplitSci1.SetIndicFore(0, highlightColor);
                doc.SplitSci2.SetIndicStyle(0, (Int32)ScintillaNet.Enums.IndicatorStyle.RoundBox);
                doc.SplitSci2.SetIndicFore(0, highlightColor);
                this.StartStyling(position, mask);
                this.SetStyling(end - start, mask);
                this.StartStyling(this.EndStyled, mask - 1);
            }
        }

        /// <summary>
        /// Removes the highlights from the control
        /// </summary>
        public void RemoveHighlights()
        {
            Int32 mask = (1 << this.StyleBits);
            this.StartStyling(0, mask);
            this.SetStyling(this.TextLength, 0);
            this.StartStyling(this.EndStyled, mask - 1);
        }

        /// <summary>
        /// Move the current line (or selected lines) up
        /// </summary>
        public void MoveLineUp()
        {
            MoveLine(-1);
        }

        /// <summary>
        /// Move the current line (or selected lines) down
        /// </summary>
        public void MoveLineDown()
        {
            MoveLine(1);
        }

        /// <summary>
        /// Moves the current line(s) up or down
        /// </summary>
        public void MoveLine(int dir)
        {
            int start = this.SelectionStart < this.SelectionEnd ? this.SelectionStart : this.SelectionEnd;
            int end = this.SelectionStart > this.SelectionEnd ? this.SelectionStart : this.SelectionEnd;
            int startLine = this.LineFromPosition(start);
            int endLine = this.LineFromPosition(end);
            // selection was not made in whole lines, so extend the end of selection to the start of the next line
            if (this.PositionFromLine(endLine) != end || startLine == endLine) ++endLine;
            if (this.SelectionStart == this.SelectionEnd && PluginBase.MainForm.Settings.CodingStyle == CodingStyle.BracesAfterLine)
            {
                string str = this.GetLine(startLine).Trim();
                if (str.StartsWith("{")) startLine = this.GetStartLine(startLine - 1);
                else if (str.IndexOf('(') >= 0)
                {
                    int pos = this.GetLine(startLine).IndexOf('(');
                    pos += this.PositionFromLine(startLine);
                    pos = this.BraceMatch(pos);
                    if (pos != -1 /*INVALID_POSITION*/)
                    {
                        int nextLine = this.LineFromPosition(pos);
                        if (this.GetLine(nextLine + 1).Trim().StartsWith("{")) endLine = nextLine + 2;
                    }
                }
            }
            int len = endLine - startLine;
            this.BeginUndoAction();
            this.SelectionStart = this.PositionFromLine(startLine);
            this.SelectionEnd = this.PositionFromLine(endLine);
            string selectStr = this.SelText;
            int saveEndAtLastLine = EndAtLastLine;
            this.EndAtLastLine = 0; // setting this to 0 prevents unwanted scrolling jumps when moving lines near the bottom of file
            this.Clear();
            if (dir > 0)
            {
                if (startLine + 1 >= this.LineCount)
                {
                    String eol = LineEndDetector.GetNewLineMarker(EOLMode);
                    this.AppendText(eol.Length, eol);
                }
                this.LineDown();
            }
            else this.LineUp();
            startLine += dir;
            // line # moved past limits, so back out the change
            if (startLine < 0 || startLine >= this.LineCount) startLine -= dir;
            else
            {
                int ctrlBlock = this.IsControlBlock(selectStr);
                if (ctrlBlock != 0)
                {
                    if (ConfigurationLanguage == "xml" || ConfigurationLanguage == "html" || ConfigurationLanguage == "css")
                    {
                        if (ctrlBlock < 0 && (selectStr.IndexOf("</") >= 0 || selectStr.IndexOf("/>") >= 0)) ctrlBlock = 0;
                        else if (len > 1) ctrlBlock = 0;
                    }
                    else
                    {
                        char oppositeMark = (ctrlBlock < 0) ? '}' : '{';
                        if (selectStr.IndexOf(oppositeMark) >= 0) ctrlBlock = 0;    // selection contains both open and close marks, so clear the setting
                    }
                }
                // if we're moving a single control block start/end, reindent the affected lines that are moving in or out of the block
                if (ctrlBlock != 0)
                {
                    int line = startLine;
                    if (dir > 0) --line;
                    int indent = dir * this.Indent;
                    if (ctrlBlock < 0)indent = -indent;
                    this.SetLineIndentation(line, this.GetLineIndentation(line) + indent);
                }
            }
            start = this.PositionFromLine(startLine);
            this.InsertText(start, selectStr.TrimEnd() + LineEndDetector.GetNewLineMarker(EOLMode));
            this.ReindentLines(startLine, len);
            this.SelectionStart = start;
            this.SelectionEnd = this.LineEndPosition(startLine + len - 1);
            this.EndAtLastLine = saveEndAtLastLine;
            this.EndUndoAction();
        }

        /// <summary>
        /// Reindents a block of pasted or moved lines to match the indentation of the destination pos
        /// </summary>
        public void ReindentLines(int startLine, int nLines)
        {
            if (nLines <= 0 || nLines > 200) return;
            String pasteStr = "";
            String destStr = "";
            int commentIndent = -1;
            int pasteIndent = -1;
            int indent;
            int line;
            // find first non-comment line above the paste, so we can properly recolorize the affected area, even if it spans block comments
            for (line = startLine; line > 0; )
            {
                --line;
                if (!PositionIsOnComment(PositionFromLine(line)))
                {
                    break;
                }
            }
            Colourise(PositionFromLine(line), PositionFromLine(startLine + nLines));
            // Scan pasted lines to find their indentation
            for (line = startLine; line < startLine + nLines; ++line)
            {
                pasteStr = GetLine(line).Trim();
                if (pasteStr != "")
                {
                    indent = GetLineIndentation(line);
                    if (PositionIsOnComment(PositionFromLine(line) + indent))
                    {
                        // Indent of the first commented line
                        if (commentIndent < 0) commentIndent = indent;

                    }
                    else // We found code, so we won't be using comment-based indenting
                    {
                        commentIndent = -1;
                        pasteIndent = indent;
                        break;
                    }
                }
            }
            // Scan the destination to determine its indentation
            int destIndent = -1;
            for (line = startLine; --line >= 0; )
            {
                destStr = GetLine(line).Trim();
                if (destStr != "")
                {
                    if (pasteIndent < 0)
                    {
                        // no code lines were found in the paste, so use the comment indentation
                        pasteIndent = commentIndent;
                        destIndent = GetLineIndentation(line);  // destination indent at any non-blank line
                        if (IsControlBlock(destStr) < 0) destIndent = GetLineIndentation(GetStartLine(line)) + Indent;
                        break;
                    }
                    else
                    {
                        if (!IsComment(destStr))
                        {
                            destIndent = GetLineIndentation(line); // destination indent at first code-line
                            if (IsControlBlock(destStr) < 0)
                            {
                                destIndent = GetLineIndentation(GetStartLine(line));
                                // Indent when we're pasting at the start of a control block (unless we're pasting an end block),
                                if (IsControlBlock(pasteStr) <= 0) destIndent += Indent;
                            }
                            else
                            {
                                // Outdent when we're pasting the end of a control block anywhere but after the start of a control block
                                if (IsControlBlock(pasteStr) > 0) destIndent -= Indent;
                            }
                            if (true) // TODO: Should test a config value for indenting after "case:" statements?
                            {
                                if (CodeEndsWith(destStr, ":") && !CodeEndsWith(FirstLine(pasteStr), ":"))
                                {
                                    // If dest line ends with ":" and paste line doesn't
                                    destIndent += Indent;
                                }
                                if (CodeEndsWith(FirstLine(pasteStr), ":") && CodeEndsWith(destStr, ";"))
                                {
                                    // If paste line ends with ':' and dest line doesn't
                                    destIndent -= Indent;
                                }
                            }
                            break;
                        }
                    }
                }
            }
            if (pasteIndent < 0) pasteIndent = 0;
            if (destIndent < 0) destIndent = 0;
            while (--nLines >= 0)
            {
                indent = GetLineIndentation(startLine);
                if (indent >= Indent || !PositionIsOnComment(PositionFromLine(startLine)))   // TODO: Are there any other lines besides comments that we want to keep in column 1? (preprocessor, ??)
                {                                                                            // Note that any changes here must also be matched when determining pasteIndent.
                    SetLineIndentation(startLine, destIndent + indent - pasteIndent);
                }
                ++startLine;
            }
        }

        /// <summary>
        /// Returns the starting line of a multi-line context (like function parameters, or long XML tags)
        /// </summary>
        public int GetStartLine(int line)
        {
            string str = GetLine(line);
            char marker;
            marker = (ConfigurationLanguage == "xml" || ConfigurationLanguage == "html" || ConfigurationLanguage == "css") ? '>' : ')';
            int pos = str.LastIndexOf(marker);
            if (pos >= 0)
            {
                pos += PositionFromLine(line);
                pos = BraceMatch(pos);
                if (pos != -1 /*INVALID_POSITION*/)
                {
                    line = LineFromPosition(pos);
                }
            }
            return line;
        }

        /// <summary>
        /// Determines whether the input string starts with a comment
        /// </summary>
        public bool IsComment(string str)
        {
            bool ret;
            String lineComment = Configuration.GetLanguage(ConfigurationLanguage).linecomment;
            String blockComment = Configuration.GetLanguage(ConfigurationLanguage).commentstart;
            ret = ((!String.IsNullOrEmpty(lineComment) && str.StartsWith(lineComment)) || (!String.IsNullOrEmpty(blockComment) && str.StartsWith(blockComment)));
            return ret;
        }
 
        /// <summary>
        /// Determines whether the input string is a start/end of a control block
        /// Returns -1:start, 1:end, 0:neither
        /// </summary>
        public int IsControlBlock(string str)
        {
            int ret = 0;
            str = str.Trim();
            if (str.Length == 0) return ret;
            // TODO: Is there a lexer test for "start/end of control block"?
            if (ConfigurationLanguage == "xml" || ConfigurationLanguage == "html" || ConfigurationLanguage == "css")
            {
                if (str.StartsWith("</")) ret = 1;
                else if (!str.StartsWith("<?") && !str.StartsWith("<!") && !str.Contains("</") && !str.EndsWith("/>") && str.EndsWith(">")) ret = -1;
            }
            else
            {
                if (str[0] == '}') ret = 1;
                else if (CodeEndsWith(str, "{")) ret = -1;
            }
            return ret;
        }

        /// <summary>
        /// Tests whether the code-portion of a string ends with a string value
        /// </summary>
        public bool CodeEndsWith(string str, string value)
        {
            bool ret = false;
            int startIndex = str.LastIndexOf(value);
            if (startIndex >= 0)
            {
                String lineComment = Configuration.GetLanguage(ConfigurationLanguage).linecomment;
                if (!String.IsNullOrEmpty(lineComment))
                {
                    int slashIndex = str.LastIndexOf(lineComment);
                    if (slashIndex >= startIndex) str = str.Substring(0, slashIndex);
                }
                if (str.Trim().EndsWith(value)) ret = true;
            }
            return ret;
        }

        /// <summary>
        /// Returns the first line of a string
        /// </summary>
        public string FirstLine(string str)
        {
            char newline = (EOLMode == 1) ? '\r' : '\n';
            int eol = str.IndexOf(newline);
            if (eol < 0) return str;
            else return str.Substring(0, eol);
        }

        /// <summary>
        /// Select the word at the caret location.
        /// </summary>
        public void SelectWord()
        {
            int startPos = this.WordStartPosition(this.CurrentPos, true);
            int endPos = this.WordEndPosition(this.CurrentPos, true);
            this.SetSel(startPos, endPos);
        }

        /// <summary>
        /// Copy the word at the caret location.
        /// </summary>
        public void CopyWord()
        {
            this.SelectWord();
            this.Copy();
        }

        /// <summary>
        /// Replace-paste the word at the caret location.
        /// </summary>
        public void ReplaceWord()
        {
            this.BeginUndoAction();
            this.SelectWord();
            this.Paste();
            this.EndUndoAction();
        }

        /// <summary>
        /// Cut the selection, if selection empty cut the line with the caret
        /// </summary>
        public void CutAllowLine()
        {
            if (this.SelTextSize == 0) this.LineCut();
            else this.Cut();
        }

        /// <summary>
        /// Cut the selection, if selection empty cut the line with the caret
        /// </summary>
        public void CutAllowLineEx()
        {
            if (this.SelTextSize == 0 && this.GetLine(this.LineFromPosition(this.CurrentPos)).Trim() != "")
            {
                this.LineCut();
            }
            else this.Cut();
        }

        /// <summary>
        /// Cut the selection, if selection empty cut the line with the caret
        /// </summary>
        public void CopyAllowLineEx()
        {
            if (this.SelTextSize == 0 && this.GetLine(this.LineFromPosition(this.CurrentPos)).Trim() != "")
            {
                this.CopyAllowLine();
            }
            else this.Copy();
        }

        /// <summary>
        /// Gets the word to the left of the cursor
        /// </summary>
        public string GetWordLeft(int position, bool skipWS)
        {
            char c;
            string word = "";
            string lang = this.ConfigurationLanguage;
            Language config = ScintillaControl.Configuration.GetLanguage(lang);
            string characterClass = config.characterclass.Characters;
            while (position >= 0)
            {
                c = (char)this.CharAt(position);
                if (c <= ' ')
                {
                    if (!skipWS) break;
                }
                else if (characterClass.IndexOf(c) < 0) break;
                else
                {
                    word = c + word;
                    skipWS = false;
                }
                position--;
            }
            return word;
        }

		#endregion

    }
	
}
