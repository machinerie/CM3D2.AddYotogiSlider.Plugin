using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityInjector.Attributes;

using UnityObsoleteGui;
using PV = UnityObsoleteGui.PixelValuesCM3D2;


namespace CM3D2.AddYotogiSlider.Plugin
{

    [PluginFilter("CM3D2x64"), PluginFilter("CM3D2x86"), PluginFilter("CM3D2VRx64")]
    [PluginName("CM3D2 AddYotogiSlider"), PluginVersion("0.0.2.3")]
    public class AddYotogiSlider : UnityInjector.PluginBase
    {
        public const string PluginName = "AddYotogiSlider";
        public const string Version    = "0.0.2.3";

        private readonly float InitPerTime        = 1f;
        private readonly float ReflectionPerTime  = 0.025f;
        private readonly string commandUnitName = "/UI Root/YotogiPlayPanel/CommandViewer/SkillViewer/MaskGroup/SkillGroup/CommandParent/CommandUnit";



        #region Variables

        private int   sceneLevel;
        private bool  visible             = false;
        private bool  initCompleted       = false;
        private float fPassedTimeOnLevel  = 0f;
        private float fLastInitTime       = 0f;
        private float fLastReflectionTime = 0f;
        private bool  bCursorOnWindow     = false;
        private bool  writeLog            = false;
        private bool  canStart 
        {
            get
            {  
                return initCompleted && isActive(goCommandUnit) 
                && yotogiPlayManager && yotogiPlayManager.fade_status == WfScreenChildren.FadeStatus.Wait;
            }
        }
        private bool  kagScriptCallbacksOverride = false;

        private string[] sKey =  { "WIN", "STATUS", "AHE", "BOTE", "FACEBLEND", "FACEANIME"};
        private string[] sliderName = {"興奮", "精神", "理性", "感度", "瞳Y", "腹"};
        private Dictionary<string, PlayAnime> pa = new Dictionary<string, PlayAnime>();

        private Window   window;
        private Rect     winRatioRect = new Rect(0.75f, 0.25f, 0.20f, 0.65f);
        private Rect     winAnimeRect;
        private float[]  fWinAnimeFrom;
        private float[]  fWinAnimeTo;

        private Dictionary<string, YotogiPanel>      panel  = new Dictionary<string, YotogiPanel>();
        private Dictionary<string, YotogiSlider>     slider = new Dictionary<string, YotogiSlider>();
        private Dictionary<string, YotogiButtonGrid> grid   = new Dictionary<string, YotogiButtonGrid>();
        private Dictionary<string, YotogiToggle>     toggle = new Dictionary<string, YotogiToggle>();

        private int   iLastExcite            = 0;
        private int   iOrgasmCount           = 0;
        private int   iLastSliderFrustration = 0;
        private float fLastSliderSensitivity = 0f;
        private float fPassedTimeOnCommand   = -1f;

        //AutoAHE
        private float    fEyePosToSliderMul  = 5000f;
        private int      idxAheOrgasm   { get{ return (int)Math.Min(Math.Floor(iOrgasmCount / 3f), 2); } }//絶頂回数3,6で変化
        private int[]    iAheExcite          = new int[] { 267, 233, 200 };                               //適用の興奮閾値
        private float    fAheDefEye          = 0f;
        private float    fAheLastEye         = 0f;
        private float    fAheEyeDecrement    = 0.20f / 60f;                                               //放置時の瞳降下
        private float[]  fAheNormalEyeMax    = new float[] { 40f, 45f, 50f };                             //通常時の瞳の最大値
        private float[]  fAheOrgasmEyeMax    = new float[] { 50f, 60f, 70f };                             //絶頂時の瞳の最大値
        private float[]  fAheOrgasmEyeMin    = new float[] { 30f, 35f, 40f };                             //絶頂時の瞳の最小値
        private string[] fAheOrgasmFace      = new string[] { "エロ放心", "エロ好感３", "通常射精後１" }; //絶頂時のFace
        private string[] fAheOrgasmFaceBlend = new string[] { "頬１涙１", "頬２涙２", "頬３涙３よだれ" }; //絶頂時のFaceBlend

        //AutoBOTE
        private int iDefHara;             //腹の初期値
        private int iHaraIncrement = 10;  //一回の腹の増加値
        private int iBoteHaraMax   = 100; //腹の最大値
        private int iBoteCount     = 0;   //中出し回数
        
        //FaceNames
        private string[] sFaceNames = 
        {
        "エロ通常１", "エロ通常２", "エロ通常３", "エロ羞恥１", "エロ羞恥２", "エロ羞恥３",
        "エロ興奮０", "エロ興奮１", "エロ興奮２", "エロ興奮３", "エロ緊張",   "エロ期待",
        "エロ好感１", "エロ好感２", "エロ好感３", "エロ我慢１", "エロ我慢２", "エロ我慢３",
        "エロ嫌悪１", "エロ怯え",   "エロ痛み１", "エロ痛み２", "エロ痛み３", "エロメソ泣き",
        "エロ絶頂",  "エロ痛み我慢", "エロ痛み我慢２","エロ痛み我慢３", "エロ放心", "発情",
        "通常射精後１", "通常射精後２", "興奮射精後１", "興奮射精後２", "絶頂射精後１", "絶頂射精後２",
        "エロ舐め愛情", "エロ舐め愛情２", "エロ舐め快楽", "エロ舐め快楽２", "エロ舐め嫌悪", "エロ舐め通常",
        "閉じ舐め愛情", "閉じ舐め快楽", "閉じ舐め快楽２", "閉じ舐め嫌悪", "閉じ舐め通常", "接吻", 
        "エロフェラ愛情", "エロフェラ快楽", "エロフェラ嫌悪", "エロフェラ通常", "エロ舌責", "エロ舌責快楽",
        "閉じフェラ愛情", "閉じフェラ快楽", "閉じフェラ嫌悪", "閉じフェラ通常", "閉じ目",   "目口閉じ",
        "通常", "怒り", "笑顔", "微笑み", "悲しみ２", "泣き",  
        "きょとん", "ジト目","あーん", "ためいき", "ドヤ顔", "にっこり", 
        "びっくり", "ぷんすか", "まぶたギュ", "むー", "引きつり笑顔", "疑問",
        "苦笑い", "困った", "思案伏せ目", "少し怒り", "誘惑",  "拗ね", 
        "優しさ","居眠り安眠","目を見開いて","痛みで目を見開いて", "余韻弱","目口閉じ",
        "口開け","恥ずかしい","照れ", "照れ叫び","ウインク照れ", "にっこり照れ",
        "ダンス目つむり","ダンスあくび","ダンスびっくり","ダンス微笑み","ダンス目あけ","ダンス目とじ",
        "ダンスウインク", "ダンスキス", "ダンスジト目","ダンス困り顔", "ダンス真剣","ダンス憂い",
        "ダンス誘惑", "頬０涙０", "頬０涙１", "頬０涙２", "頬０涙３", "頬１涙０", 
        "頬１涙１",   "頬１涙２", "頬１涙３", "頬２涙０", "頬２涙１", "頬２涙２", 
        "頬２涙３",   "頬３涙１", "頬３涙０", "頬３涙２", "頬３涙３", "追加よだれ",
        "頬０涙０よだれ", "頬０涙１よだれ", "頬０涙２よだれ", "頬０涙３よだれ", "頬１涙０よだれ", "頬１涙１よだれ",
        "頬１涙２よだれ", "頬１涙３よだれ", "頬２涙０よだれ", "頬２涙１よだれ", "頬２涙２よだれ", "頬２涙３よだれ",
        "頬３涙０よだれ", "頬３涙１よだれ", "頬３涙２よだれ", "頬３涙３よだれ" , "エラー", "デフォ", 
        };
        private string[] sFaceBlendCheek = new string[]{"頬０", "頬１", "頬２", "頬３"};
        private string[] sFaceBlendTear  = new string[]{"涙０", "涙１", "涙２", "涙３"};

        // ゲーム内部変数への参照
        private Maid maid;
        private FieldInfo maidStatusInfo;
        private FieldInfo maidFoceKuchipakuSelfUpdateTime;
        private GameObject goCommandUnit;
        private YotogiParamBasicBar yotogiParamBasicBar;
        private YotogiPlayManager yotogiPlayManager;
        private Action<Yotogi.SkillData.Command.Data> orgOnClickCommand;
        private KagScript kagScript;
        private Func<KagTagSupport, bool> orgTagFace;
        private Func<KagTagSupport, bool> orgTagFaceBlend;

        #endregion



        #region Nested classes

        private class YotogiPanel : Container
        {
            public enum HeaderUI
            {
                None,
                Slider,
                Face
            }

            private Rect     padding          { get{ return PV.PropRect(paddingPx); } }
            private int      paddingPx        = 4;
            private GUIStyle labelStyle       = "label";
            private GUIStyle toggleStyle      = "toggle";
            private GUIStyle buttonStyle      = "button";
            private string   headerHeightPV   = "C1";
            private string   headerFontSizePV = "C1";
            private HeaderUI headerUI;
            private bool     childrenVisible = false;

            public string Title;
            public string HeaderUILabelText;
            public bool   Enabled        = false;
            public bool   HeaderUIToggle = false;

            public YotogiPanel(string name, string title) : this(name, title, HeaderUI.None) {}
            public YotogiPanel(string name, string title, HeaderUI type)
            : base(name, new Rect (Window.AutoLayout, Window.AutoLayout, Window.AutoLayout, 0))
            {
                this.Title    = title;
                this.headerUI = type;
                Resize();
            }

            public override void Draw(Rect outRect)
            {
                Rect groupRect = PV.InsideRect(outRect, padding);
                
                labelStyle = "box";
                GUI.Label(outRect, "", labelStyle);
                GUI.BeginGroup(groupRect);
                {
                    int headerHeight   = PV.Line(headerHeightPV);
                    int headerFontSize = PV.Font(headerFontSizePV);

                    Rect cur = new Rect(0f, 0f, padding.width, headerHeight);

                    cur.width = groupRect.width * 0.325f;
                    buttonStyle.fontSize = headerFontSize;
                    resizeOnChangeChildrenVisible( GUI.Toggle(cur, childrenVisible, Title, buttonStyle) );
                    cur.x += cur.width;

                    cur.width = groupRect.width * 0.300f;
                    cur.y -= PV.PropPx(2);
                    toggleStyle.fontSize         = headerFontSize;
                    toggleStyle.alignment        = TextAnchor.MiddleLeft;
                    toggleStyle.normal.textColor = toggleColor(Enabled);
                    toggleStyle.hover.textColor  = toggleColor(Enabled);
                    Enabled = GUI.Toggle(cur, Enabled, toggleText(Enabled), toggleStyle);
                    cur.y += PV.PropPx(2);
                    cur.x += cur.width;

                    labelStyle = "label";
                    labelStyle.fontSize = headerFontSize;
                    switch (headerUI)
                    {
                        case HeaderUI.Slider:
                        {
                            cur.width = groupRect.width * 0.375f;
                            labelStyle.alignment = TextAnchor.MiddleRight;
                            GUI.Label(cur, "Pin", labelStyle);
                        }
                        break;
                        
                        case HeaderUI.Face:
                        {
                            cur.width = groupRect.width * 0.375f;
                            labelStyle = "box";
                            labelStyle.fontSize = headerFontSize;
                            labelStyle.alignment = TextAnchor.MiddleRight;
                            GUI.Label(cur, HeaderUILabelText, labelStyle);
                        }
                        break;

                        default: break;
                    }

                    cur.x = 0;
                    cur.y += cur.height + + PV.PropPx(3);
                    cur.width = groupRect.width;

                    foreach (Element child in children)
                    {
                        if (!(child.Visible)) continue;

                        cur.height = child.Height;
                        child.Draw(cur);
                        cur.y += cur.height + PV.PropPx(3);
                    }
                }
                GUI.EndGroup();
            }

            public override void Resize() { Resize(false); } 
            public override void Resize(bool broadCast)
            {
                float height = PV.Line(headerHeightPV) + PV.PropPx(3);

                foreach (Element child in children) if (child.Visible) height += child.Height + PV.PropPx(3);
                rect.height = height + (int)padding.height * 2;

                if (!broadCast) notifyParent(true, false);
            }

            //----

            private void resizeOnChangeChildrenVisible(bool b)
            {
                if (b != childrenVisible)
                {
                    foreach(Element child in children)  child.Visible = b;
                    childrenVisible = b;
                }
            }

            private Color toggleColor(bool b) { return b ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 0.2f, 0.2f, 1f);  }
            private string toggleText(bool b) { return b ? "Enabled" : "Disabled"; }
        }


        private class YotogiSlider : Element
        {
            private HSlider  slider;
            private string   lineHeightPV = "C1";
            private string   fontSizePV   = "C1";
            private GUIStyle labelStyle   = "label";
            private string   labelText    = "";
            private bool     pinEnabled   = false;

            public float Value { get{ return slider.Value; } set{ if(!Pin) slider.Value = value; } }
            public float Default;
            public bool  Pin;

            public YotogiSlider(string name, float min, float max, float def, EventHandler<SliderEventArgs> onChange, string label, bool pinEnabled)
            : base(name, new Rect (Window.AutoLayout, Window.AutoLayout, Window.AutoLayout, 0))
            {
                this.slider     = new HSlider(name+":slider", rect, min, max, def, onChange);
                this.Default    = def;
                this.labelText  = label;
                this.pinEnabled = pinEnabled;
                Resize();
            }

            public override void Draw(Rect outRect)
            {
                Rect cur = outRect;
                labelStyle = "label";

                cur.width = outRect.width * 0.1625f;
                labelStyle.fontSize = PV.Font(fontSizePV);
                labelStyle.alignment = TextAnchor.MiddleCenter;
                GUI.Label(cur, labelText, labelStyle);
                cur.x += cur.width;

                cur.width = outRect.width * 0.1575f;
                labelStyle = "box";
                labelStyle.fontSize = PV.Font(fontSizePV);
                labelStyle.alignment = TextAnchor.MiddleRight;
                GUI.Label(cur, slider.Value.ToString("F0"), labelStyle);
                cur.x += cur.width + outRect.width * 0.005f;

                cur.width = outRect.width * 0.60f;
                cur.y += PV.PropPx(4);
                slider.Draw(cur);
                cur.y -= PV.PropPx(4);
                cur.x += cur.width;

                if (pinEnabled) 
                {
                    cur.width = outRect.width * 0.075f;
                    cur.y -= PV.PropPx(2);
                    Pin = GUI.Toggle(cur, Pin, "");
                }
            }

            public override void Resize() { Resize(false); } 
            public override void Resize(bool broadCast) { rect.height = PV.Line(lineHeightPV); }
        }

        private class YotogiToggle : Element
        {
            private bool     val;
            private Toggle   toggle;
            private GUIStyle labelStyle   = "label";
            private GUIStyle toggleStyle  = "toggle";
            private string   lineHeightPV = "C1";
            private string   fontSizePV   = "C1";

            public bool Value { get{ return toggle.Value; } }
            public string LabelText;

            public YotogiToggle(string name, bool def, string text, EventHandler<ToggleEventArgs> onChange)
            : base(name, new Rect (Window.AutoLayout, Window.AutoLayout, Window.AutoLayout, 0))
            {
                this.toggle    = new Toggle(name+":toggle", rect, def, text, onChange);
                this.val       = def;
                this.LabelText = text;
                Resize();
            }

            public override void Draw(Rect outRect)
            {
                Rect cur = outRect;

                cur.width = outRect.width * 0.5f;
                labelStyle.fontSize  = PV.Font(fontSizePV);
                labelStyle.alignment = TextAnchor.MiddleLeft;
                GUI.Label(cur, LabelText, labelStyle);
                cur.x += cur.width;

                cur.width = outRect.width * 0.5f;
                toggle.Style.fontSize         = PV.Font(fontSizePV);
                toggle.Style.alignment        = TextAnchor.MiddleLeft;
                toggle.Style.normal.textColor = toggleColor(toggle.Value);
                toggle.Style.hover.textColor  = toggleColor(toggle.Value);
                toggle.Content.text           = toggleText(toggle.Value);
                cur.y -= PV.PropPx(2);
                toggle.Draw(cur);
            }

            public override void Resize() { Resize(false); } 
            public override void Resize(bool broadCast) { rect.height = PV.Line(lineHeightPV); }
            
            private Color toggleColor(bool b) { return b ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 0.2f, 0.2f, 1f);  }
            private string toggleText(bool b) { return b ? "Enabled" : "Disabled"; }
        }

        private class YotogiButtonGrid : Element
        {
            private string[] buttonNames;
            private GUIStyle labelStyle    = "box";
            private GUIStyle toggleStyle   = "toggle";
            private GUIStyle buttonStyle   = "button";
            private string   lineHeightPV  = "C1";
            private string   fontSizePV    = "C1";
            private int      columns       = 2;
            private int      spacerPx      = 5;
            private int      spacerPerRow  = 3;
            private int      spacerPerCol  = -1;
            private bool     tabEnabled    = false;
            private int      tabSelected   = -1;
            private Vector2  scrollViewVector = Vector2.zero;
            private SelectButton[] selectButton;
            
            public bool   GirdToggle = false;
            public string GirdLabelText = "";

            public event EventHandler<ButtonEventArgs> OnClick;
                
            public YotogiButtonGrid(string name, string[] buttonNames, EventHandler<ButtonEventArgs> _onClick, bool tabEnabled)
            : base(name, new Rect (Window.AutoLayout, Window.AutoLayout, Window.AutoLayout, 0))
            {
                this.buttonNames = buttonNames;
                this.OnClick    += _onClick;
                this.tabEnabled  = tabEnabled;
                
                if (tabEnabled)
                {
                    selectButton = new SelectButton[2]
                        { new SelectButton("SelectButton:Cheek", rect, new string[4]{"頬０", "頬１", "頬２", "頬３"}, this.OnSelectButtonFaceBlend),
                          new SelectButton("SelectButton:Tear",  rect, new string[4]{"涙０", "涙１", "涙２", "涙３"}, this.OnSelectButtonFaceBlend)};
                    onChangeTab(0);
                }
                
                Resize();
            }

            public override void Draw(Rect outRect)
            {
                int spacer  = PV.PropPx(spacerPx);
                int btnNum  = buttonNames.Length;
                int tabLine = PV.Line(lineHeightPV) + PV.PropPx(3);
                int rowNum  = (int)Math.Ceiling((double)btnNum / columns);

                GUI.BeginGroup(outRect);
                {
                    Rect cur = new Rect(0, 0, outRect.width, PV.Line(lineHeightPV));
                    
                    if (tabEnabled)
                    {
                        cur.width  = outRect.width * 0.3f;
                        toggleStyle.fontSize         = PV.Font(fontSizePV);
                        toggleStyle.alignment        = TextAnchor.MiddleLeft;
                        toggleStyle.normal.textColor = toggleColor(GirdToggle);
                        toggleStyle.hover.textColor  = toggleColor(GirdToggle);
                        onClickDroolToggle( GUI.Toggle(cur, GirdToggle, "よだれ", toggleStyle) );
                        cur.x += cur.width;

                        cur.width  = outRect.width * 0.7f;
                        onChangeTab( GUI.Toolbar(cur, tabSelected, new string[2]{ "頬・涙・涎", "全種Face"}, buttonStyle) );
                        
                        cur.x  = 0f;
                        cur.y += cur.height + PV.PropPx(3);
                        cur.width = outRect.width;
                    }

                    if (!tabEnabled || tabSelected == 1)
                    {
                        Rect scrlRect    = new Rect(cur.x, cur.y, cur.width, outRect.height - (tabEnabled ? tabLine : 0));
                        Rect contentRect = new Rect(0f, 0f, outRect.width - PV.Sys_("HScrollBar.Width") - spacer,
                                                    PV.Line(lineHeightPV) * rowNum + spacer * (int)(rowNum / spacerPerRow));

                        scrollViewVector = GUI.BeginScrollView(scrlRect, scrollViewVector, contentRect, false, true);
                        {
                            Rect scrlCur = new Rect(0, 0, contentRect.width / columns, PV.Line(lineHeightPV));
                            int row = 1, col = 1;

                            foreach(string buttonName in buttonNames)
                            {
                                onClick( GUI.Button(scrlCur,buttonName), buttonName );

                                if (columns > 0 && col == columns)
                                {
                                    scrlCur.x  = 0;
                                    scrlCur.y += scrlCur.height;
                                    if (spacerPerRow > 0 && row % spacerPerRow == 0) scrlCur.y += spacer;
                                    row++;
                                    col = 1;
                                } 
                                else
                                {
                                    scrlCur.x += scrlCur.width;
                                    if (spacerPerCol > 0 && col % spacerPerCol == 0) scrlCur.x += spacer;
                                    col++;
                                }
                            }
                        }
                        GUI.EndScrollView();

                    }
                    else if (tabSelected == 0)
                    {
                        selectButton[0].Draw(cur);
                        cur.y += cur.height;
                        selectButton[1].Draw(cur);
                    }
                    
                }
                GUI.EndGroup();
            }
            
            public override void Resize() { Resize(false); } 
            public override void Resize(bool broadCast)
            {
                int spacer  = PV.PropPx(spacerPx);
                int tabLine = PV.Line(lineHeightPV) + PV.PropPx(3);

                if (!tabEnabled)           rect.height = PV.Line(lineHeightPV) * 6 + spacer;
                else if (tabSelected == 0) rect.height = tabLine + PV.Line(lineHeightPV) * 2;
                else if (tabSelected == 1) rect.height = tabLine + PV.Line(lineHeightPV) * 6 + spacer;
                
                if (!broadCast) notifyParent(true, false);
            }

            public void OnSelectButtonFaceBlend(object sb, SelectEventArgs args) 
            {
                if (((YotogiPanel)Parent).Enabled) 
                {
                    string senderName = args.Name;
                    string faceName   = args.ButtonName;
                    
                    if (senderName == "SelectButton:Cheek")     faceName = faceName + selectButton[1].Value;
                    else if (senderName == "SelectButton:Tear") faceName = selectButton[0].Value + faceName;
                    if (GirdToggle) faceName += "よだれ";
                    
                    OnClick(this, new ButtonEventArgs(this.name, faceName));
                }
            }
            
            //----

            private void onClickDroolToggle(bool b) 
            {
                if (b != GirdToggle)
                {
                    string faceName = selectButton[0].Value + selectButton[1].Value + (b ? "よだれ" : "");
                    OnClick(this, new ButtonEventArgs(this.name, faceName));
                    GirdToggle = b;
                }
            }

            private void onChangeTab(int i) 
            {
                if (i != tabSelected)
                {
                    tabSelected = i;
                    Resize();
                }
            }

            private void onClick(bool click, string s) 
            {
                if (click) OnClick(this, new ButtonEventArgs(this.name, s));
            }
            
             private Color toggleColor(bool b) { return b ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 0.2f, 0.2f, 1f); }
        }


        private class PlayAnime
        {
            public AddYotogiSlider Parent = null;
            public string Key  = "";
            public string Name = "";
            public float[] Value;
            public bool NowPlaying { get{ return play && (passedTime < finishTime); } }
            public bool Finished   { get{ return (passedTime >= finishTime); } }
            public bool SetterExist{ get{ return (num == 1) ? !IsNull(setValue0) : !IsNull(setValue); } }

            private float[] vFrom;
            private float[] vTo;
            private int   type       = 1;
            private int   num        = 1;
            private bool  play       = false;
            private float passedTime = 0f;
            private float startTime  = 0f;
            private float finishTime = 0f;
            //private float[] actionTime;

            private Func<float>     progress  = null;
            private Action<float>   setValue0 = null;
            private Action<float[]> setValue  = null;
            
            public PlayAnime(AddYotogiSlider p, string name, int n, float st, float ft)
            {
                Parent      = p; 
                Name        = name; 
                Key         = (name.Split('.'))[0];
                num         = n;
                Value       = new float[n];
                vFrom       = new float[n];
                vTo         = new float[n];
                startTime   = st;
                finishTime  = ft;
                progress    = ( ) => (passedTime - startTime) / (finishTime - startTime);
            }

            public PlayAnime(AddYotogiSlider p, string name, int n, float st, float ft, int t)  : this(p, name, n, st, ft)
            {
                type        = t;
            }

            public bool IsKye(string s)    { return s == Key; }
            public bool Contains(string s) { return Name.Contains(s); }

            public void SetFrom(float vform)            { vFrom[0] = vform; }
            public void SetTo(float vto)                { vTo[0]   = vto; } 
            public void SetSetter(Action<float> func)   { setValue0 = func; }
            public void Set(float vform, float vto)     { SetFrom(vform); SetTo(vto); } 

            public void SetFrom(float[] vform)          { if(vform.Length == num) Array.Copy(vform ,vFrom, num); } 
            public void SetTo(float[] vto)              { if(vto.Length   == num) Array.Copy(vto,   vTo,   num); }
            public void SetSetter(Action<float[]> func) { setValue = func; } 
            public void Set(float[] vform, float[] vto) { SetFrom(vform); SetTo(vto); } 

            public void Play()
            {
                if (SetterExist)
                {
                    passedTime = 0f;
                    play = true;
                }
            } 
            public void Play(float vform, float vto)     { Set(vform, vto); Play(); }
            public void Play(float[] vform, float[] vto) { Set(vform, vto); Play(); }

            public void Stop() { play = false; } 

            public void Update()
            {
                if (play)
                {
                    bool change = false;
                    
                    for(int i=0; i<num; i++)
                    {
                        if (vFrom[i] == vTo[i]) continue;
                        
                        if (passedTime >= finishTime)
                        {
                            Stop();
                        }
                        else if (passedTime >= startTime)
                        {
                            switch (type)
                            {
                                case 1 :
                                {
                                    Value[i] = vFrom[i] + (vTo[i] - vFrom[i]) * progress();
                                    change = true;
                                }
                                break;
                                case 2 :
                                {
                                    Value[i] = vFrom[i] + (vTo[i] - vFrom[i]) * Mathf.Pow(progress(), 2);
                                    change = true;
                                }
                                break;
                                default : break;
                            }
                            if(Parent.writeLog) Debug.LogError("PlayAnime["+Name+"].Update :"+ Value[i]);
                        }
                    }

                    if (change)
                    {
                        if(num == 1) setValue0(Value[0]);
                        else         setValue(Value);
                    }
                }

                passedTime += Time.deltaTime;
            }
        }

        #endregion



        #region MonoBehaviour methods

        public void Awake()
        {
            pa["WIN.Load"]    = new PlayAnime(this, "WIN.Load",    2, 0.00f,  0.25f, 2);
            pa["AHE.継続.0"]  = new PlayAnime(this, "AHE.継続.0",  1, 0.00f,  0.75f);
            pa["AHE.絶頂.0"]  = new PlayAnime(this, "AHE.絶頂.0",  1, 6.00f,  9.00f);
            pa["BOTE.絶頂"]   = new PlayAnime(this, "BOTE.絶頂",   1, 0.00f,  6.00f);
            pa["BOTE.止める"] = new PlayAnime(this, "BOTE.止める", 1, 0.00f,  4.00f);
        }
        

        public void OnLevelWasLoaded(int level)
        {
            fPassedTimeOnLevel = 0f;

            if (sceneLevel == 14 && sceneLevel != level) finalize();

            sceneLevel = level;
        }

        public void Update()
        {
            fPassedTimeOnLevel += Time.deltaTime;
            if (fPassedTimeOnCommand >= 0f) fPassedTimeOnCommand += Time.deltaTime;

            if (sceneLevel == 14)
            {
                if (!initCompleted && (fPassedTimeOnLevel - fLastInitTime > InitPerTime))
                {
                    initCompleted = initialize();
                    fLastInitTime = fPassedTimeOnLevel;
                }

                if (!canStart) return;

                if (Input.GetKeyDown(KeyCode.F5))
                {
                    winAnimeRect = window.Rectangle;
                    visible = !visible;
                    playAnimeOnInputKeyDown(KeyCode.F5);
                }
                

                if (fPassedTimeOnLevel - fLastInitTime > ReflectionPerTime)
                {
                    fLastReflectionTime = fPassedTimeOnLevel;
                }
                updateAnimeOnUpdate();
            }
        }

        public void OnGUI()
        {
            if (sceneLevel == 14 && canStart)
            {
                updateAnimeOnGUI();
                if (visible && !pa["WIN.Load"].NowPlaying) 
                {
                    updateCameraControl();
                    window.Draw();
                }
            }
        }

        #endregion



        #region Callbacks

        public void OnYotogiPlayManagerOnClickCommand(Yotogi.SkillData.Command.Data command_data)
        {
            iLastExcite = maid.Param.status.cur_excite;
            fLastSliderSensitivity = slider["Sensitivity"].Value;
            iLastSliderFrustration = getSliderFrustration();
            fPassedTimeOnCommand = 0f;
            
            if (panel["Status"].Enabled) updateMaidFrustration(iLastSliderFrustration);
            initAnimeOnCommand();

            orgOnClickCommand(command_data);

            playAnimeOnCommand(command_data.basic);
            syncSlidersOnClickCommand(command_data.status);

            if (command_data.basic.command_type == Yotogi.SkillCommandType.絶頂) 
            {
                if (!panel["FaceAnime"].Enabled && pa["AHE.絶頂.0"].NowPlaying)
                {
                    maid.FaceAnime(fAheOrgasmFace[idxAheOrgasm], 5f, 0);
                    panel["FaceAnime"].HeaderUILabelText = fAheOrgasmFace[idxAheOrgasm];
                }
            }
        }

        public bool OnYotogiKagManagerTagFace(KagTagSupport tag_data)
        {
            if (panel["FaceAnime"].Enabled || pa["AHE.絶頂.0"].NowPlaying) 
            {
                return false;
            }
            else 
            {
                panel["FaceAnime"].HeaderUILabelText = tag_data.GetTagProperty("name").AsString();
                return orgTagFace(tag_data);
            }
        }

        public bool OnYotogiKagManagerTagFaceBlend(KagTagSupport tag_data)
        {
            if (panel["FaceBlend"].Enabled || pa["AHE.絶頂.0"].NowPlaying) 
            {
                return false;
            }
            else 
            {
                panel["FaceBlend"].HeaderUILabelText = tag_data.GetTagProperty("name").AsString();
                return orgTagFaceBlend(tag_data);
            }
        }

        //----
        
        public void OnChangeSliderExcite(object ys, SliderEventArgs args)
        {
            if (panel["Status"].Enabled) updateMaidExcite((int)args.Value);
        }

        public void OnChangeSliderMind(object ys, SliderEventArgs args)
        {
            if (panel["Status"].Enabled) updateMaidMind((int)args.Value);
        }

        public void OnChangeSliderReason(object ys, SliderEventArgs args)
        {
            if (panel["Status"].Enabled) updateMaidReason((int)args.Value);
        }

        public void OnChangeSliderSensitivity(object ys, SliderEventArgs args)
        {
            ;
        }

        public void OnChangeSliderEyeY(object ys, SliderEventArgs args)
        {
            updateMaidEyePosY(args.Value);
        }
        
        public void OnChangeSliderHara(object ys, SliderEventArgs args)
        {
            updateMaidHaraValue((int)args.Value);
        }

        public void OnChangeToggleLipsync(object tgl, ToggleEventArgs args)
        {
            updateMaidFoceKuchipakuSelfUpdateTime(args.Value);
        }

        public void OnClickButtonFaceAnime(object ygb, ButtonEventArgs args)
        {
            if (panel["FaceAnime"].Enabled) 
            {
               maid.FaceAnime(args.ButtonName, 1f, 0);
               panel["FaceAnime"].HeaderUILabelText = args.ButtonName;
            }
        }

        public void OnClickButtonFaceBlend(object ysg, ButtonEventArgs args)
        {
            if (panel["FaceBlend"].Enabled) 
            {
                maid.FaceBlend(args.ButtonName);
                panel["FaceBlend"].HeaderUILabelText = args.ButtonName;
            }
        }
        

        #endregion



        #region Private methods

        private bool initialize()
        {
            this.goCommandUnit = GameObject.Find(commandUnitName); // 夜伽コマンド画面かどうか
            if (!this.goCommandUnit) return false;

            this.maid = GameMain.Instance.CharacterMgr.GetMaid(0);
            if (!this.maid) return false;
            
            this.maidStatusInfo = getFieldInfo<MaidParam>("status_");
            if (IsNull(this.maidStatusInfo)) return false;

            this.maidFoceKuchipakuSelfUpdateTime = getFieldInfo<Maid>("m_bFoceKuchipakuSelfUpdateTime");
            if (IsNull(this.maidFoceKuchipakuSelfUpdateTime)) return false;

            this.yotogiParamBasicBar = getInstance<YotogiParamBasicBar>();
            if (!this.yotogiParamBasicBar) return false;


            // 夜伽コマンドフック
            {
                this.yotogiPlayManager = getInstance<YotogiPlayManager>(); 
                if (!this.yotogiPlayManager) return false;

                YotogiCommandFactory cf = getFieldValue<YotogiPlayManager, YotogiCommandFactory>(this.yotogiPlayManager, "command_factory_");
                if (IsNull(cf)) return false;

                try {
                cf.SetCommandCallback(new YotogiCommandFactory.CommandCallback(this.OnYotogiPlayManagerOnClickCommand));
                } catch(Exception ex) { Debug.LogError(AddYotogiSlider.PluginName +" : SetCommandCallback() : "+ ex); return false; }

                this.orgOnClickCommand = getMethodDelegate<YotogiPlayManager, Action<Yotogi.SkillData.Command.Data>>(this.yotogiPlayManager, "OnClickCommand");
                if (IsNull(this.orgOnClickCommand)) return false;
            }

            // Face・FaceBlendフック
            {
                YotogiKagManager ykm = GameMain.Instance.ScriptMgr.yotogi_kag;
                if (IsNull(ykm)) return false;

                this.kagScript = getFieldValue<YotogiKagManager, KagScript>(ykm, "kag_");
                if (IsNull(this.kagScript)) return false;
                
                try{
                this.kagScript.RemoveTagCallBack("face");
                this.kagScript.AddTagCallBack("face", new KagScript.KagTagCallBack(this.OnYotogiKagManagerTagFace));
                this.kagScript.RemoveTagCallBack("faceblend");
                this.kagScript.AddTagCallBack("faceblend", new KagScript.KagTagCallBack(this.OnYotogiKagManagerTagFaceBlend));
                kagScriptCallbacksOverride = true;
                } catch(Exception ex) { Debug.LogError(AddYotogiSlider.PluginName +" : kagScriptCallBack() : "+ ex);  return false; }

                this.orgTagFace = getMethodDelegate<YotogiKagManager, Func<KagTagSupport, bool>>(ykm, "TagFace");
                this.orgTagFaceBlend = getMethodDelegate<YotogiKagManager, Func<KagTagSupport, bool>>(ykm, "TagFaceBlend");
                if (IsNull(this.orgTagFace)) return false;
            }

            // PlayAnime
            {
                foreach(KeyValuePair<string, PlayAnime> o in pa)
                {
                    PlayAnime p = o.Value;
                    if (!p.SetterExist) 
                    {
                        if (p.Contains("WIN"))  p.SetSetter(updateWindowAnime);
                        if (p.Contains("AHE"))  p.SetSetter(updateMaidEyePosY);
                        if (p.Contains("BOTE")) p.SetSetter(updateMaidHaraValue);
                    }
                }
                fAheDefEye   = maid.body0.trsEyeL.localPosition.y * fEyePosToSliderMul;
                iDefHara     = maid.GetProp("Hara").value;
                iBoteCount   = 0;
                iOrgasmCount = 0;
            }


            // Window
            {
                window = new Window(winRatioRect, AddYotogiSlider.Version, "Yotogi Slider");

                float mind        = (float)maid.Param.status.mind;
                float reason      = (float)maid.Param.status.reason;
                float sensitivity = maid.Param.status.correction_data.excite + maid.Param.status.frustration;

                slider["Excite"]      = new YotogiSlider("Slider:Excite",       -100f, 300f,   0f,              this.OnChangeSliderExcite,      sliderName[0], true);
                slider["Mind"]        = new YotogiSlider("Slider:Mind",         0f,    mind,   mind,            this.OnChangeSliderMind,        sliderName[1], true);
                slider["Reason"]      = new YotogiSlider("Slider:Reason",       0f,    reason, reason,          this.OnChangeSliderReason,      sliderName[2], true);
                slider["Sensitivity"] = new YotogiSlider("Slider:Sensitivity",  -100f, 200f,   sensitivity,     this.OnChangeSliderSensitivity, sliderName[3], true);
                slider["EyeY"]        = new YotogiSlider("Slider:EyeY",         0f,    100f,   fAheDefEye,      this.OnChangeSliderEyeY,        sliderName[4], false);
                slider["Hara"]        = new YotogiSlider("Slider:Hara",         0f,    150f,   (float)iDefHara, this.OnChangeSliderHara,        sliderName[5], false);

                toggle["Lipsync"]  = new YotogiToggle("Toggle:Lipsync",  false, " Lipsync cancelling", this.OnChangeToggleLipsync);

                grid["FaceAnime"] = new YotogiButtonGrid("GridButton:FaceAnime", sFaceNames, this.OnClickButtonFaceAnime, false);
                grid["FaceBlend"] = new YotogiButtonGrid("GridButton:FaceBlend", sFaceNames, this.OnClickButtonFaceBlend, true);

                slider["EyeY"].Visible = false;
                slider["Hara"].Visible = false;
                toggle["Lipsync"].Visible = false;
                grid["FaceAnime"].Visible = false;
                grid["FaceBlend"].Visible = false;


                panel["Status"] = window.AddChild<YotogiPanel>( new YotogiPanel("Panel:Status", "Status", YotogiPanel.HeaderUI.Slider) );
                panel["Status"].AddChild(slider["Excite"]);
                panel["Status"].AddChild(slider["Mind"]);
                panel["Status"].AddChild(slider["Reason"]);
                panel["Status"].AddChild(slider["Sensitivity"]);

                window.AddHorizontalSpacer();

                panel["AutoAHE"] = window.AddChild<YotogiPanel>( new YotogiPanel("Panel:AutoAHE", "AutoAHE") );
                panel["AutoAHE"].AddChild(slider["EyeY"]);

                window.AddHorizontalSpacer();

                panel["AutoBOTE"] = window.AddChild<YotogiPanel>( new YotogiPanel("Panel:AutoBOTE", "AutoBOTE") );
                panel["AutoBOTE"].AddChild(slider["Hara"]);
                window.AddHorizontalSpacer();

                panel["FaceAnime"] = window.AddChild<YotogiPanel>( new YotogiPanel("Panel:FaceAnime", "FaceAnime", YotogiPanel.HeaderUI.Face) );
                panel["FaceAnime"].AddChild(toggle["Lipsync"]);
                panel["FaceAnime"].AddChild(grid["FaceAnime"]);
                
                window.AddHorizontalSpacer();
                
                panel["FaceBlend"] = window.AddChild<YotogiPanel>( new YotogiPanel("Panel:FaceBlend", "FaceBlend", YotogiPanel.HeaderUI.Face) );
                panel["FaceBlend"].AddChild(grid["FaceBlend"]);
            }

            Debug.Log(AddYotogiSlider.PluginName + " : Initialization complete.");
            return true;
        }

        private void finalize()
        {
                try{
            visible = false;

            window  = null;
            panel.Clear();
            slider.Clear();
            grid.Clear();
            toggle.Clear();

            initCompleted = false;
            fLastInitTime = 0f;
            fPassedTimeOnCommand = -1f;

            iLastExcite              = 0;
            iOrgasmCount             = 0;
            iLastSliderFrustration   = 0;
            fLastSliderSensitivity   = 0f;
            
            maid.SetProp("Hara", iDefHara, false);
            iDefHara   = 0;
            iBoteCount = 0;

            goCommandUnit                   = null;
            maid                            = null;
            maidStatusInfo                  = null;
            maidFoceKuchipakuSelfUpdateTime = null;
            yotogiParamBasicBar             = null;
            yotogiPlayManager               = null;
            orgOnClickCommand               = null;

            if (kagScriptCallbacksOverride)
            {
                kagScript.RemoveTagCallBack("face");
                kagScript.AddTagCallBack("face", new KagScript.KagTagCallBack(this.orgTagFace));
                kagScript.RemoveTagCallBack("faceblend");
                kagScript.AddTagCallBack("faceblend", new KagScript.KagTagCallBack(this.orgTagFaceBlend));
                kagScriptCallbacksOverride = false;

                kagScript       = null;
                orgTagFace      = null;
                orgTagFaceBlend = null;
            }
            } catch(Exception ex) { Debug.LogError(AddYotogiSlider.PluginName +" : finalize() : "+ ex);  return; }

        }

        //----
        
        private void syncSlidersOnClickCommand(Yotogi.SkillData.Command.Data.Status cmStatus)
        {
            if (!slider["Excite"].Pin) slider["Excite"].Value = (float)maid.Param.status.cur_excite;
            else updateMaidExcite((int)slider["Excite"].Value);
            
            if (!slider["Mind"].Pin)   slider["Mind"].Value   = (float)maid.Param.status.cur_mind;
            else updateMaidMind((int)slider["Mind"].Value);

            if (!slider["Reason"].Pin) slider["Reason"].Value = (float)maid.Param.status.cur_reason;
            else updateMaidReason((int)slider["Reason"].Value);

            slider["Sensitivity"].Value = (float)( maid.Param.status.correction_data.excite
                + (panel["Status"].Enabled ? iLastSliderFrustration + cmStatus.frustration : maid.Param.status.frustration)
                + (maid.Param.status.cur_reason < 20 ? 20 : 0) );

            slider["EyeY"].Value = maid.body0.trsEyeL.localPosition.y * fEyePosToSliderMul;
            slider["Hara"].Value = (float)maid.GetProp("Hara").value;
        }

        private void initAnimeOnCommand()
        {
            if (panel["AutoAHE"].Enabled) 
            {
                fAheLastEye = maid.body0.trsEyeL.localPosition.y * fEyePosToSliderMul;
 
                for (int i=0; i<1; i++)
                {
                    if (pa["AHE.絶頂."+ i].NowPlaying) pa["AHE.絶頂."+ i].Stop();
                    if (pa["AHE.継続."+ i].NowPlaying) pa["AHE.継続."+ i].Stop();
                }
            }

            if (panel["AutoBOTE"].Enabled) 
            {
                // アニメ再生中にコマンド実行で強制的に終端値に
                if (pa["BOTE.絶頂"].NowPlaying) 
                {
                    updateMaidHaraValue(Mathf.Min(iDefHara + iHaraIncrement * iBoteCount, iBoteHaraMax));
                }
                if (pa["BOTE.止める"].NowPlaying) 
                {
                    updateMaidHaraValue(iDefHara);
                }
                
                pa["BOTE.絶頂"].Stop();
                pa["BOTE.止める"].Stop();
            }
        }

        private void playAnimeOnCommand(Yotogi.SkillData.Command.Data.Basic data)
        {
            if (panel["AutoAHE"].Enabled) 
            {
                float excite = maid.Param.status.cur_excite;
                int i = idxAheOrgasm;
                
                if (data.command_type == Yotogi.SkillCommandType.絶頂)
                {
                    if (iLastExcite >= iAheExcite[i])
                    {
                        pa["AHE.継続.0"].Play(fAheLastEye ,fAheOrgasmEyeMax[i]);
                        pa["AHE.絶頂.0"].Play(fAheOrgasmEyeMax[i], fAheOrgasmEyeMin[i]);
                        iOrgasmCount++;
                    }
                }
                else
                {
                    if (excite >= iAheExcite[i])
                    {
                        float to = fAheNormalEyeMax[i] * (excite - iAheExcite[i]) / (300f - iAheExcite[i]);
                        pa["AHE.継続.0"].Play(fAheLastEye, to);
                    }
                }
            }

            if (panel["AutoBOTE"].Enabled) 
            {
                float from = (float)maid.GetProp("Hara").value;
                
                if (data.command_type == Yotogi.SkillCommandType.絶頂)
                {
                    if (data.name.Contains("中出し") || data.name.Contains("注ぎ込む"))
                    {
                        iBoteCount++;
                        float to = Mathf.Min(iDefHara + iHaraIncrement * iBoteCount, iBoteHaraMax);
                        pa["BOTE.絶頂"].Play(from, to);
                    }
                    else if (data.name.Contains("外出し"))
                    {
                        pa["BOTE.止める"].Play(from, iDefHara);
                        iBoteCount = 0;
                    }
                }
                else if (data.command_type == Yotogi.SkillCommandType.止める)
                {
                    pa["BOTE.止める"].Play(from, iDefHara);
                    iBoteCount = 0;
                }
            }
        }

        private void playAnimeOnInputKeyDown(KeyCode keycode)
        {
            if (keycode == KeyCode.F5)
            {
                if (visible)
                {
                    fWinAnimeFrom = new float[2] { Screen.width, 0f };
                    fWinAnimeTo   = new float[2] { winAnimeRect.x , 1f };
                }
                else
                {
                    fWinAnimeFrom = new float[2] { winAnimeRect.x, 1f };
                    fWinAnimeTo   = new float[2] { (winAnimeRect.x + winAnimeRect.width / 2> Screen.width / 2f) ? Screen.width : -winAnimeRect.width, 0f };
                }
                pa["WIN.Load"].Play(fWinAnimeFrom, fWinAnimeTo);
            }
        }

        private void updateAnimeOnUpdate()
        {
            if (panel["AutoAHE"].Enabled) 
            {
                if (pa["AHE.継続.0"].NowPlaying) pa["AHE.継続.0"].Update();
                
                if (pa["AHE.絶頂.0"].NowPlaying) 
                {
                    pa["AHE.絶頂.0"].Update();
                    maid.FaceBlend(fAheOrgasmFaceBlend[idxAheOrgasm]);
                    panel["FaceBlend"].HeaderUILabelText = fAheOrgasmFaceBlend[idxAheOrgasm];
                }
                
                if (!pa["AHE.継続.0"].NowPlaying && !pa["AHE.絶頂.0"].NowPlaying)
                {
                    float eyepos = maid.body0.trsEyeL.localPosition.y * fEyePosToSliderMul;
                    if (eyepos > fAheDefEye) updateMaidEyePosY(eyepos - fAheEyeDecrement * (int)(fPassedTimeOnCommand / 10));
                }
            }
            
            if (panel["AutoBOTE"].Enabled)
            {
                if (pa["BOTE.絶頂"].NowPlaying)   pa["BOTE.絶頂"].Update();
                if (pa["BOTE.止める"].NowPlaying) pa["BOTE.止める"].Update();
            }

        }

        private void updateAnimeOnGUI() 
        {
            if (pa["WIN.Load"].NowPlaying) 
            { 
                pa["WIN.Load"].Update();
            }
        }

        private void dummyWin(int winID) {}

        //----

        private void updateSlider(string name, float value)
        {
            Container.Find<YotogiSlider>(window, name).Value = value;
        }

        private void updateWindowAnime(float[] x)
        {
            winAnimeRect.x = x[0];
            GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, x[1]);
            
            GUIStyle winStyle = "box";
            winStyle.fontSize = PV.Font("C1");
            winStyle.alignment = TextAnchor.UpperRight;
            winAnimeRect = GUI.Window(0, winAnimeRect, dummyWin, AddYotogiSlider.Version, winStyle);
        }

        private void updateMaidExcite(int value)
        {
            maid.Param.SetCurExcite(value);
            yotogiParamBasicBar.SetCurrentExcite(value, true);
            iLastExcite = maid.Param.status.cur_excite;
        }

        private void updateMaidMind(int value)
        {
            maid.Param.SetCurMind(value);
            yotogiParamBasicBar.SetCurrentMind(value, true);
        }

        private void updateMaidReason(int value)
        {
            maid.Param.SetCurReason(value);
            yotogiParamBasicBar.SetCurrentReason(value, true);
        }

        private void updateMaidFrustration(int value)
        {
            param.Status tmp = (param.Status)maidStatusInfo.GetValue(maid.Param);
            tmp.frustration = value - maid.Param.status.correction_data.excite - (maid.Param.status.cur_reason < 20 ? 20 : 0);
            maidStatusInfo.SetValue(maid.Param, tmp);
        }

        private void updateMaidEyePosY(float value)
        {
            if (value < 0f) value = 0f;
            Vector3 vl = maid.body0.trsEyeL.localPosition;
            Vector3 vr = maid.body0.trsEyeR.localPosition;
            maid.body0.trsEyeL.localPosition = new Vector3(vl.x, Math.Max((fAheDefEye + value)/fEyePosToSliderMul, 0f), vl.z);
            maid.body0.trsEyeR.localPosition = new Vector3(vl.x, Math.Min((fAheDefEye - value)/fEyePosToSliderMul, 0f), vl.z);

            updateSlider("Slider:EyeY", value);
        }

        private void updateMaidHaraValue(float value)
        {
            try {
            maid.SetProp("Hara", (int)value, false);
            maid.body0.VertexMorph_FromProcItem("hara", value/100f);
            } catch { /*Debug.LogError(AddYotogiSlider.PluginName +" : "+ ex);*/ }
            
            updateSlider("Slider:Hara", value);
        }
        
        private void updateMaidFoceKuchipakuSelfUpdateTime(bool b)
        {
            maidFoceKuchipakuSelfUpdateTime.SetValue(maid, b);
        }

        private void updateCameraControl()
        {
            Vector2 cursor = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            bool b = window.Rectangle.Contains(cursor);
            if (b != bCursorOnWindow)
            {
                GameMain.Instance.MainCamera.SetControl(!b);
                UICamera.InputEnable = !b;
                bCursorOnWindow = b;
            }
        }

        private int getSliderFrustration()
        {
            return (int)(slider["Sensitivity"].Value - maid.Param.status.correction_data.excite + (maid.Param.status.cur_reason < 20 ? 20 : 0));
        }

        #endregion



        #region Utility methods

        internal static bool IsNull<T>(T t) where T : class
        {
            return (t == null) ? true : false;
        }

        internal static bool IsNull<T>(T t, string s) where T : class
        {
            if(t == null)
            {
                Debug.LogError(AddYotogiSlider.PluginName +" : "+ s);
                return false;
            }
            else return true;
        }

        internal static bool isActive(GameObject go)
        {
            return go ? go.activeInHierarchy : false;
        }

        internal static T getInstance<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectOfType(typeof(T)) as T;
        }

        internal static TResult getMethodDelegate<T, TResult>(T inst, string name) where T : class where TResult : class
        {
            return Delegate.CreateDelegate(typeof(TResult), inst, name) as TResult;
        }

        internal static FieldInfo getFieldInfo<T>(string name)
        {
            BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

            return typeof(T).GetField(name, bf);
        }

        internal static TResult getFieldValue<T, TResult>(T inst, string name)
        {
            if (inst == null)  return default(TResult);

            FieldInfo field = getFieldInfo<T>(name);
            if (field == null) return default(TResult);

            return (TResult)field.GetValue(inst);
        }

        #endregion
        
    }

}




namespace UnityObsoleteGui
{

    public abstract class Element : IComparable<Element>
    {
        protected readonly int id;

        protected string name;
        protected Rect   rect;
        protected bool   visible;

        public string Name      { get{ return name; } }
        
        public virtual Rect   Rectangle { get{ return rect; } }
        public virtual float  Left      { get{ return rect.x; } }
        public virtual float  Top       { get{ return rect.y; } }
        public virtual float  Width     { get{ return rect.width; } }
        public virtual float  Height    { get{ return rect.height; } }
        public virtual bool Visible 
        { 
            get{ return visible; } 
            set
            { 
                visible = value; 
                if (Parent != null) notifyParent(false, true); 
            } 
        }

        public Container Parent = null;
        public event EventHandler<ElementEventArgs> NotifyParent = delegate{};


        public Element() {}
        public Element(string name, Rect rect)
        {
            this.id      = this.GetHashCode();
            this.name    = name; 
            this.rect    = rect;
            this.visible = true;
        }

        public virtual void Draw() { Draw(this.rect); }
        public virtual void Draw(Rect outRect) {}
        public virtual void Resize() { Resize(false); }
        public virtual void Resize(bool broadCast) { if(!broadCast) notifyParent(true, false); }

        public virtual int CompareTo(Element e) { return this.name.CompareTo(e.Name); }

        protected virtual void notifyParent(bool sizeChanged, bool visibleChanged) 
        {
            NotifyParent(this, new ElementEventArgs(name, sizeChanged, visibleChanged));
        }
    }


    public abstract class Container : Element, IEnumerable<Element>
    {
        public static Element Find(Container parent, string s) { return Container.Find<Element>(parent, s); }
        public static T Find<T>(Container parent, string s) where T : Element
        {
            if (parent == null) return null;

            foreach (Element e in parent)
            {
                if (e is T && e.Name == s) return e as T;
                if (e is Container) 
                {
                    T e2 = Find<T>(e as Container, s);
                    if (e2 != null) return e2 as T;
                }
            }

            return null;
        }

        //----
        
        protected List<Element> children = new List<Element>();

        public int ChildCount { get{ return children.Count; } }


        public Container(string name, Rect rect) : base(name, rect) {}

        public Element this[string s]
        {
            get { return GetChild<Element>(s); }
            set { if (value is Element) AddChild(value); }
        }

        public Element AddChild(Element child) { return AddChild<Element>(child); }
        public T AddChild<T>(T child) where T : Element
        {
            if (child != null && !children.Contains(child))
            {
                child.Parent = this;
                child.NotifyParent += this.onChildChenged;
                children.Add(child);
                Resize();
                
                return child;
            }
            
            return null;
        }

        public Element GetChild(string s)        { return GetChild<Element>(s); }
        public T GetChild<T>() where T : Element { return GetChild<T>(""); }
        public T GetChild<T>(string s) where T : Element
        {
            return children.FirstOrDefault(e => e is T && (s == "" ? true : e.Name == s)) as T;
        }

        public void RemoveChild(string s)
        {
            Element child = children.FirstOrDefault(e => e.Name == s);
            if (child != null) 
            {
                child.Parent = null;
                child.NotifyParent -= this.onChildChenged;
                children.Remove(child);
                Resize();
            }
        }
        
        public void RemoveChildren()
        {
            foreach (Element child in children)
            {
                child.Parent = null;
                child.NotifyParent -= this.onChildChenged;
            }
            children.Clear();
            Resize();
        }
        
        public virtual void onChildChenged(object sender, EventArgs e) { Resize(); }

        IEnumerator IEnumerable.GetEnumerator()     { return this.GetEnumerator(); }
        public IEnumerator<Element> GetEnumerator() { return children.GetEnumerator(); }

    }
    

    public class Window : Container
    {

        #region Constants
        public const float AutoLayout = -1f;

        [Flags]
        public enum Scroll
        {
            None    = 0x00,
            HScroll = 0x01,
            VScroll = 0x02
        }

        #endregion



        #region Nested classes

        private class HorizontalSpacer : Element
        {
            public HorizontalSpacer(float height) 
            : base("Spacer:", new Rect(Window.AutoLayout, Window.AutoLayout, Window.AutoLayout, height) )
            {
                this.name += this.id;
            }
        }

        #endregion



        #region Variables

        private Rect sizeRatio;
        private Rect baseRect;
        private Rect titleRect;
        private Rect contentRect;
        private Vector2 autoSize       = Vector2.zero;
        private Vector2 hScrollViewPos = Vector2.zero;
        private Vector2 vScrollViewPos = Vector2.zero;
        private Vector2 lastScreenSize;
        private int colums = 1;

        public GUIStyle WindowStyle = "window";
        public GUIStyle LabelStyle  = "label";
        public string   HeaderText;
        public int      HeaderFontSize;
        public string   TitleText;
        public float    TitleHeight;
        public int      TitleFontSize;
        public Scroll   scroll = Scroll.None;

        #endregion



        #region Methods

        public Window(Rect ratio, string header, string title)              : this(title, ratio, header, title, null) {}
        public Window(string name, Rect ratio, string header, string title) : this(name,  ratio, header, title, null) {}
        public Window(string name, Rect ratio, string header, string title, List<Element> children) : base(name, PV.PropScreenMH(ratio))
        {
            this.sizeRatio  = ratio;
            this.HeaderText = header;
            this.TitleText  = title;
            this.TitleHeight= PV.Line("C1");

            if (children != null && children.Count > 0) 
            {
                this.children = new List<Element>(children);
                foreach (Element child in children) 
                {
                    child.Parent = this;
                    child.NotifyParent += this.onChildChenged;

                }
                Resize();
            }
            
            lastScreenSize = new Vector2(Screen.width, Screen.height);
        }

        public override void Draw(Rect outRect)
        {
            if (propScreen())
            {
                resizeAllChildren(this);
                Resize();
                outRect = rect;
            }

            WindowStyle.fontSize  = PV.Font("C2");
            WindowStyle.alignment = TextAnchor.UpperRight;

            rect = GUI.Window(id, outRect, drawWindow, HeaderText, WindowStyle);
        }

        public override void Resize()
        {
            calcAutoSize();
        }

        public Element AddHorizontalSpacer()             { return AddHorizontalSpacer((float)PV.Margin); }
        public Element AddHorizontalSpacer(float height) { return AddChild( new HorizontalSpacer(height) ); }

        //----
        
        private void drawWindow(int id)
        {
            TitleHeight    = PV.Line("C1");
            TitleFontSize  = PV.Font("C2");

            LabelStyle.fontSize = TitleFontSize;
            LabelStyle.alignment = TextAnchor.UpperLeft;
            GUI.Label(titleRect, TitleText, LabelStyle);
            
            GUI.BeginGroup(contentRect);
            {
                Rect cur = new Rect(0f, 0f, 0f, 0f);
                
                foreach (Element child in children)
                {
                    if (!child.Visible) continue;

                    if (child.Left >= 0 || child.Top >= 0)
                    {
                        Rect tmp = new Rect ( (child.Left  >= 0) ? child.Left   : cur.x,
                                              (child.Top   >= 0) ? child.Top    : cur.y,
                                              (child.Width  > 0) ? child.Width  : autoSize.x,
                                              (child.Height > 0) ? child.Height : autoSize.y);

                        child.Draw(tmp);
                    }
                    else
                    {
                        cur.width  = (child.Width  > 0) ? child.Width  : autoSize.x;
                        cur.height = (child.Height > 0) ? child.Height : autoSize.y;
                        child.Draw(cur);
                        cur.y += cur.height;
                    }
                }
            }
            GUI.EndGroup();
            
            GUI.DragWindow();
        }

        private bool propScreen()
        {
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            if (lastScreenSize != screenSize)
            {
                rect = PV.PropScreenMH(rect.x, rect.y, sizeRatio.width , sizeRatio.height, lastScreenSize);
                lastScreenSize = screenSize;
                calcRectSize();
                return true;
            }
            return false;
        }
        
        private void calcRectSize()
        {
            baseRect    = PV.InsideRect(rect);
            titleRect   = new Rect(PV.Margin, 0, baseRect.width, TitleHeight);
            contentRect = new Rect(baseRect.x, baseRect.y + titleRect.height, baseRect.width, baseRect.height - titleRect.height);
        }

        public void calcAutoSize()
        {
            Vector2 used  = Vector2.zero;
            Vector2 count = Vector2.zero;
            
            foreach (Element child in children)
            {
                if (!child.Visible) continue;
                
                if (!(child.Left > 0 || child.Top > 0) && child.Width > 0)  used.x += child.Width;
                else count.x += 1;

                if (!(child.Left > 0 || child.Top > 0) && child.Height > 0) used.y += child.Height;
                else count.y += 1;
            }

            {
                bool rectChanged = false;

                if ((scroll & Window.Scroll.HScroll) == 0x00)
                {
                    if (contentRect.width < used.x || (contentRect.width > used.x && count.x == 0))
                    {
                        rect.width = used.x + PV.Margin * 2;
                        rectChanged = true;
                    }
                }

                if ((scroll & Window.Scroll.VScroll) == 0x00)
                {
                    if (contentRect.height < used.y || (contentRect.height > used.y && count.y == 0))
                    {
                        rect.height = used.y + titleRect.height + PV.Margin * 3;
                        rectChanged = true;
                    }
                }
            
                if (rectChanged) calcRectSize();
            }

            autoSize.x = (count.x > 0) ? (contentRect.width  - used.x) / colums : contentRect.width;
            autoSize.y = (count.y > 0) ? (contentRect.height - used.y) / (float)Math.Ceiling(count.y/colums) : contentRect.height;
        }
        
        private void resizeAllChildren(Container parent) 
        {
            if (parent == null) return;
            
            foreach(Element child in parent)
            {
                if (child is Container) resizeAllChildren(child as Container);
                else child.Resize(true);
            }
        }

        #endregion

    }


    public class HSlider : Element
    {
        public GUIStyle Style      = "horizontalSlider";
        public GUIStyle ThumbStyle = "horizontalSliderThumb";
        public float Value;
        public float Min;
        public float Max;

        public event EventHandler<SliderEventArgs> OnChange;

        public HSlider(string name, Rect rect, float min, float max, float def, EventHandler<SliderEventArgs> _OnChange) : base(name, rect)
        {
            this.Value     = def;
            this.Min       = min;
            this.Max       = max;
            this.OnChange += _OnChange;
        }

        public override void Draw(Rect outRect)
        {
            onChange( GUI.HorizontalSlider(outRect, Value, Min, Max, Style, ThumbStyle) );
        }
        
        private void onChange(float newValue)
        {
            if (newValue != Value)
            {
                OnChange(this, new SliderEventArgs(name, newValue));
                Value = newValue;
            }
        }
    }

    public class Toggle : Element
    {
        private bool    val;

        public GUIStyle Style = "toggle";
        public GUIContent Content;
        public bool   Value  { get{ return val; } }
        public string Text   { get{ return Content.text; }  set{ Content.text = value; } }

        public event EventHandler<ToggleEventArgs> OnChange;

        public Toggle(string name, Rect rect, EventHandler<ToggleEventArgs> _OnChange)              : this(name, rect, false, "",   _OnChange) {}
        public Toggle(string name, Rect rect, bool def, EventHandler<ToggleEventArgs> _OnChange)    : this(name, rect, def,   "",   _OnChange) {}
        public Toggle(string name, Rect rect, string text, EventHandler<ToggleEventArgs> _OnChange) : this(name, rect, false, text, _OnChange) {}
        public Toggle(string name, Rect rect, bool def, string text, EventHandler<ToggleEventArgs> _OnChange) : base(name, rect)
        {
            this.val       = def;
            this.Content   = new GUIContent(text);
            this.OnChange += _OnChange;
        }

        public override void Draw(Rect outRect)
        {
            onChange( GUI.Toggle(outRect, Value, Content, Style) );
        }
        
        private void onChange(bool newValue)
        {
            if (newValue != val) OnChange(this, new ToggleEventArgs(name, newValue));
            val = newValue;
        }
    }

    public class SelectButton : Element
    {
        private string[] buttonNames;
        private int selected = 0;

        public int    SelectedIndex { get{ return selected; } }
        public string Value         { get{ return buttonNames[selected]; } }
        
        public event EventHandler<SelectEventArgs> OnSelect;
            
        public SelectButton(string name, Rect rect, string[] buttonNames, EventHandler<SelectEventArgs> _onSelect) : base(name, rect)
        {
            this.buttonNames = buttonNames;
            this.OnSelect   += _onSelect;
        }

        public override void Draw(Rect outRect)
        {
            onSelect( GUI.Toolbar(outRect, selected, buttonNames) );
        }

        private void onSelect(int newSelected)
        {
            if (selected != newSelected)
            {
                OnSelect(this, new SelectEventArgs(name, newSelected, buttonNames[newSelected]));
                selected = newSelected;
            }
        }
    }


    public class ElementEventArgs : EventArgs
    {
        public string Name;
        public bool   SizeChanged;
        public bool   VisibleChanged;
        
        public ElementEventArgs(string name, bool sizeChanged, bool visibleChanged)
        {
            this.Name           = name;
            this.SizeChanged    = sizeChanged;
            this.VisibleChanged = visibleChanged;
        }
    }

    public class SliderEventArgs : EventArgs
    {
        public string Name;
        public float  Value;
        
        public SliderEventArgs(string name, float value)
        {
            this.Name  = name;
            this.Value = value;
        }
    }
    
    public class ButtonEventArgs : EventArgs
    { 
        public string Name;
        public string ButtonName;
        
        public ButtonEventArgs(string name, string buttonName)
        {
            this.Name       = name;
            this.ButtonName = buttonName;
        }
    }

    public class ToggleEventArgs : EventArgs
    { 
        public string Name;
        public bool   Value;
        
        public ToggleEventArgs(string name, bool b)
        {
            this.Name  = name;
            this.Value = b;
        }
    }

    public class SelectEventArgs : EventArgs
    { 
        public string Name;
        public int    Index;
        public string ButtonName;
        
        public SelectEventArgs(string name, int idx, string buttonName)
        {
            this.Name       = name;
            this.Index      = idx;
            this.ButtonName = buttonName;
        }
    }


    public static class PixelValuesCM3D2
    {

        #region Variables

        private static int margin = 10;
        private static Dictionary<string, int> font = new Dictionary<string, int>();
        private static Dictionary<string, int> line = new Dictionary<string, int>();
        private static Dictionary<string, int> sys =  new Dictionary<string, int>();

        public static float BaseWidth = 1280f;
        public static float PropRatio = 0.6f;
        public static int Margin { get{ return PropPx(margin); } set{ margin = value; } }

        #endregion



        #region Methods

        static PixelValuesCM3D2()
        {
            font["C1"] = 12;
            font["C2"] = 11;
            font["H1"] = 20;
            font["H2"] = 16;
            font["H3"] = 14;

            line["C1"] = 18;
            line["C2"] = 14;
            line["H1"] = 30;
            line["H2"] = 24;
            line["H3"] = 22;

            sys["Menu.Height"] = 45;
            sys["OkButton.Height"] = 95;

            sys["HScrollBar.Width"] = 15;
        }
            
        public static int Font(string key)  { return PropPx(font[key]); }
        public static int Line(string key)  { return PropPx(line[key]); }
        public static int Sys(string key)   { return PropPx(sys[key]); }

        public static int Font_(string key) { return font[key]; }
        public static int Line_(string key) { return line[key]; }
        public static int Sys_(string key)  { return sys[key]; }

        public static Rect PropScreen(Rect ratio)
        {
            return new Rect((Screen.width  - Margin * 2) * ratio.x + Margin
                           ,(Screen.height - Margin * 2) * ratio.y + Margin
                           ,(Screen.width  - Margin * 2) * ratio.width
                           ,(Screen.height - Margin * 2) * ratio.height);
        }

        public static Rect PropScreenMH(Rect ratio)
        {
            Rect r = PropScreen(ratio);
            r.y += Sys("Menu.Height");
            r.height -= (Sys("Menu.Height") + Sys("OkButton.Height"));

            return r;
        }

        public static Rect PropScreenMH(float left, float top, float width, float height, Vector2 last)
        {
            Rect r = PropScreen(new Rect((float)(left/(last.x - Margin * 2)), (float)(top/(last.y - Margin * 2)), width, height));
            r.height -= (Sys("Menu.Height") + Sys("OkButton.Height"));

            return r;
        }

        public static Rect InsideRect(Rect rect) 
        {
            return new Rect(Margin, Margin, rect.width - Margin * 2, rect.height - Margin * 2);
        }

        public static Rect InsideRect(Rect rect, int height) 
        {
            return new Rect(Margin, Margin, rect.width - Margin * 2, height);
        }

        public static Rect InsideRect(Rect rect, Rect padding) 
        {
            return new Rect(rect.x + padding.x, rect.y + padding.x, rect.width - padding.width * 2, rect.height - padding.height * 2);
        }

        public static int PropPx(int px) 
        {
            return (int)(px * (1f + (Screen.width/BaseWidth - 1f) * PropRatio));
        }

        public static Rect PropRect(int px) 
        {
            return new Rect(PropPx(px), PropPx(px), PropPx(px), PropPx(px));
        }
        #endregion

    }

}
