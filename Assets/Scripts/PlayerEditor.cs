using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 面向玩家的编辑器入口。
/// 当前继承开发编辑器能力，后续可在此类中做玩家向限制与扩展。
/// </summary>
public class PlayerEditor : PipeEditor
{
    [Header("Player Editor")]
    public bool forceBackToStartScene = true;
    public Button myLevelsButton;
    GameObject _myLevelsPanel;
    RectTransform _myLevelsContent;
    ScrollRect _myLevelsScroll;
    GameObject _sharePopup;
    InputField _shareLinkField;
    Text _shareToastText;

    void OnEnable()
    {
        AutoWireIfMissing();
        if (forceBackToStartScene)
            backSceneName = "StartScene";
        StartCoroutine(ApplyPlayerEditorModeNextFrame());
    }

    void AutoWireIfMissing()
    {
        if (cube == null) cube = FindObjectOfType<RubikCube>();
        if (puzzle == null && cube != null) puzzle = cube.GetComponent<PipePuzzle>();
        if (cam == null) cam = Camera.main;

        if (brushButtons == null || brushButtons.Length == 0)
        {
            brushButtons = new Button[10];
            for (int i = 0; i < 10; i++)
            {
                var go = GameObject.Find("Brush_" + i);
                brushButtons[i] = go != null ? go.GetComponent<Button>() : null;
            }
        }

        if (brushButtonBgs == null || brushButtonBgs.Length == 0)
        {
            brushButtonBgs = new Image[10];
            for (int i = 0; i < 10; i++)
            {
                var go = GameObject.Find("Brush_" + i);
                brushButtonBgs[i] = go != null ? go.GetComponent<Image>() : null;
            }
        }

        if (saveButton == null) saveButton = FindButton("SaveBtn");
        if (loadButton == null) loadButton = FindButton("LoadBtn");
        if (clearButton == null) clearButton = FindButton("ClearBtn");
        if (backButton == null) backButton = FindButton("BackBtn");
        if (insertButton == null) insertButton = FindButton("InsertBtn");
        if (previewButton == null) previewButton = FindButton("PreviewBtn");
        if (resetOrientButton == null) resetOrientButton = FindButton("ResetOrientBtn");

        if (levelIdField == null) levelIdField = FindInput("LevelIdField");
        if (levelNameField == null) levelNameField = FindInput("LevelNameField");
        if (moveLimitField == null) moveLimitField = FindInput("MoveLimitField");
        if (insertIndexField == null) insertIndexField = FindInput("InsertIndexField");

        if (statusText == null) statusText = FindText("Status");
        if (hintText == null) hintText = FindText("Hint");
        if (palettePanel == null)
        {
            var pg = GameObject.Find("PalettePanel");
            if (pg != null) palettePanel = pg;
        }
    }

    Button FindButton(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go.GetComponent<Button>() : null;
    }

    InputField FindInput(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go.GetComponent<InputField>() : null;
    }

    Text FindText(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go.GetComponent<Text>() : null;
    }

    IEnumerator ApplyPlayerEditorModeNextFrame()
    {
        yield return null;
        ApplyPlayerEditorMode();
    }

    void ApplyPlayerEditorMode()
    {
        if (saveButton != null)
        {
            saveButton.onClick.RemoveAllListeners();
            saveButton.onClick.AddListener(OnSaveMyLevelClicked);
            SetButtonLabel(saveButton, "保存到我的关卡");
        }

        if (loadButton != null)
        {
            loadButton.onClick.RemoveAllListeners();
            loadButton.onClick.AddListener(OnLoadMyLevelClicked);
            SetButtonLabel(loadButton, "读取我的关卡");
        }

        if (insertButton != null)
            insertButton.gameObject.SetActive(false);

        myLevelsButton = EnsureMyLevelsButton();
        if (myLevelsButton != null)
        {
            myLevelsButton.onClick.RemoveAllListeners();
            myLevelsButton.onClick.AddListener(OnMyLevelsClicked);
        }

        if (levelIdField != null && string.IsNullOrEmpty(levelIdField.text))
            levelIdField.text = GenerateDefaultId();

        SetStatus("玩家编辑器已就绪。我的关卡存储位置：" + PlayerLevelStore.GetStoragePath());
    }

    void OnSaveMyLevelClicked()
    {
        if (puzzle == null) return;

        string id = (levelIdField != null && !string.IsNullOrEmpty(levelIdField.text))
            ? levelIdField.text.Trim()
            : GenerateDefaultId();
        if (levelIdField != null) levelIdField.text = id;

        string dn = (levelNameField != null && !string.IsNullOrEmpty(levelNameField.text)) ? levelNameField.text : id;
        int lim = 0;
        if (moveLimitField != null && !string.IsNullOrEmpty(moveLimitField.text))
            int.TryParse(moveLimitField.text.Trim(), out lim);

        puzzle.SetMoveLimit(lim);
        var data = puzzle.ExportData(id, dn);
        PlayerLevelStore.Save(data);
        SetStatus("已保存到我的关卡：" + id + "（" + data.cells.Count + " 格）");
        RefreshMyLevelsPanel();
    }

    void ShowShareLinkPopup(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        var data = PlayerLevelStore.Load(id);
        if (data == null)
        {
            SetStatus("分享失败：未找到关卡 " + id);
            return;
        }

        EnsureSharePopup();
        if (_sharePopup == null || _shareLinkField == null) return;

        _shareLinkField.text = BuildShareUrl(data);
        if (_shareToastText != null) _shareToastText.text = "";
        _sharePopup.SetActive(true);
        SetStatus("已生成分享链接：" + id);
    }

    void EnsureSharePopup()
    {
        if (_sharePopup != null) return;
        var canvas = GetComponentInChildren<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var panel = new GameObject("ShareLinkPopup", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(980f, 240f);
        panel.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.13f, 0.96f);

        CreateText(panel.transform, "Title", "分享链接", 32, TextAnchor.MiddleCenter,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(-140f, 44f));

        var closeBtn = CreateButton(panel.transform, "Close", "关闭", new Color(0.42f, 0.30f, 0.30f),
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(110f, 42f));
        if (closeBtn != null) closeBtn.onClick.AddListener(() => panel.SetActive(false));

        var copyBtn = CreateButton(panel.transform, "Copy", "复制链接", new Color(0.25f, 0.55f, 0.32f),
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, -16f), new Vector2(150f, 52f));

        var inputGO = new GameObject("LinkField", typeof(RectTransform), typeof(Image), typeof(InputField));
        inputGO.transform.SetParent(panel.transform, false);
        var inputRt = inputGO.GetComponent<RectTransform>();
        inputRt.anchorMin = new Vector2(0f, 0.5f);
        inputRt.anchorMax = new Vector2(1f, 0.5f);
        inputRt.pivot = new Vector2(0.5f, 0.5f);
        inputRt.anchoredPosition = new Vector2(66f, -16f);
        inputRt.sizeDelta = new Vector2(-260f, 52f);
        inputGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.92f);

        var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(inputGO.transform, false);
        var txtRt = textGO.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(12f, 8f);
        txtRt.offsetMax = new Vector2(-12f, -8f);
        var txt = textGO.GetComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 18;
        txt.alignment = TextAnchor.MiddleLeft;
        txt.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.supportRichText = false;

        var phGO = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
        phGO.transform.SetParent(inputGO.transform, false);
        var phRt = phGO.GetComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.offsetMin = new Vector2(12f, 8f);
        phRt.offsetMax = new Vector2(-12f, -8f);
        var ph = phGO.GetComponent<Text>();
        ph.font = txt.font;
        ph.fontSize = 18;
        ph.alignment = TextAnchor.MiddleLeft;
        ph.color = new Color(0.45f, 0.45f, 0.45f, 1f);
        ph.text = "分享链接将在这里显示";

        var input = inputGO.GetComponent<InputField>();
        input.textComponent = txt;
        input.placeholder = ph;
        input.lineType = InputField.LineType.SingleLine;
        input.contentType = InputField.ContentType.Standard;
        input.readOnly = true;

        _shareToastText = CreateText(panel.transform, "Toast", "", 24, TextAnchor.MiddleLeft,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(24f, 20f), new Vector2(-48f, 36f));
        if (_shareToastText != null) _shareToastText.color = new Color(1f, 0.88f, 0.30f, 1f);

        if (copyBtn != null)
            copyBtn.onClick.AddListener(CopyShareLinkToClipboard);

        _sharePopup = panel;
        _shareLinkField = input;
        _sharePopup.SetActive(false);
    }

    void CopyShareLinkToClipboard()
    {
        if (_shareLinkField == null) return;
        GUIUtility.systemCopyBuffer = _shareLinkField.text;
        if (_shareToastText != null)
            _shareToastText.text = "已复制链接";
    }

    string BuildShareUrl(LevelData data)
    {
        string html = BuildShareHtml(data);
        return "data:text/html;charset=utf-8," + UnityWebRequestEscape(html);
    }

    string UnityWebRequestEscape(string raw)
    {
        return System.Uri.EscapeDataString(raw);
    }

    string BuildShareHtml(LevelData data)
    {
        string json = JsonUtility.ToJson(data);
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>");
        sb.Append("<title>玩家关卡分享</title><style>");
        sb.Append("body{font-family:system-ui,Segoe UI,Arial;background:radial-gradient(circle at 20% -10%,#27384f 0,#111926 52%,#0a1018 100%);color:#eaf0f8;margin:0;padding:16px;} ");
        sb.Append(".card{max-width:1220px;margin:0 auto;background:#121b28;border:1px solid #2f435f;border-radius:14px;padding:16px;} h1{margin:0 0 8px;font-size:30px;} ");
        sb.Append(".hint{opacity:.86;margin-bottom:10px;} .top{display:flex;gap:8px;flex-wrap:wrap;margin-bottom:14px;} .pill{background:#0c1420;border:1px solid #2e425d;border-radius:999px;padding:8px 12px;font-size:14px;} .ok{color:#7cec97;} .bad{color:#ff8e8e;} ");
        sb.Append(".faces{display:grid;grid-template-columns:repeat(auto-fit,minmax(370px,1fr));gap:12px;} .face{background:#0d1520;border:1px solid #31465f;border-radius:12px;padding:10px;} .face h3{margin:0 0 8px;color:#ffd982;} ");
        sb.Append(".board{display:grid;gap:6px;} .tile{position:relative;min-height:68px;border-radius:10px;padding:6px;background:#1d2a3a;border:1px solid #2e435f;display:flex;flex-direction:column;justify-content:space-between;} .tile.on{box-shadow:0 0 0 1px #66d5ff inset;background:#20344c;} .tile.empty{opacity:.25;} ");
        sb.Append(".k{font-weight:700;font-size:12px;line-height:1.2;} .meta{font-size:11px;opacity:.82;line-height:1.2;margin-top:2px;} .rot{display:flex;gap:6px;align-items:center;} ");
        sb.Append("button{background:#2f7ad3;color:#fff;border:none;border-radius:7px;padding:4px 10px;cursor:pointer;font-size:12px;} button:disabled{opacity:.5;cursor:default;} ");
        sb.Append(".dir{font-size:18px;line-height:1;} .footer{margin-top:12px;opacity:.8;} .win{position:fixed;inset:0;background:rgba(10,16,24,.78);display:none;align-items:center;justify-content:center;z-index:99;} .win.show{display:flex;} .winbox{background:#102033;border:1px solid #4d6a88;border-radius:14px;padding:22px 26px;text-align:center;} .winbox h2{margin:0 0 8px;color:#7cec97;} ");
        sb.Append("</style></head><body>");
        sb.Append("<div class='card'><h1>关卡：").Append(EscapeHtml(data.displayName)).Append("</h1>");
        sb.Append("<div class='hint'>和游戏内规则一致：旋转管道，让所有终点通水（含传送门逻辑）。</div>");
        sb.Append("<div class='top'><div id='state' class='pill bad'>未通关</div><div id='endStat' class='pill'>终点 0/0</div><div id='moveStat' class='pill'>步数 0</div></div>");
        sb.Append("<div id='root' class='faces'></div><div class='footer'>由玩家编辑器生成分享页</div></div><div id='win' class='win'><div class='winbox'><h2>通关！</h2><div>已连接所有终点</div></div></div>");
        sb.Append("<script>const level=").Append(json).Append(";const faces=['+X','-X','+Y','-Y','+Z','-Z'];let moves=0;let wasSolved=false;");
        sb.Append("function fName(n){if(n.x===1)return '+X';if(n.x===-1)return '-X';if(n.y===1)return '+Y';if(n.y===-1)return '-Y';if(n.z===1)return '+Z';return '-Z';}");
        sb.Append("function rotMod(k){if(k===2)return 2;if(k===5)return 1;return 4;}");
        sb.Append("function kName(k){const m=['Start','End','Straight','Bend','Tee','Cross','Start2','PortalA','PortalB'];return m[k]||('Kind'+k);} ");
        sb.Append("function kShort(k){const m=['S','E','I','L','T','+','S2','PA','PB'];return m[k]||'?';}");
        sb.Append("function oriChar(o){return ['→','↑','←','↓'][o&3];}");
        sb.Append("function e2(kind,ori){ori=ori&3;if(kind===2)return ori===0?[[-.5,0],[.5,0]]:[[0,-.5],[0,.5]];if(kind===3){if(ori===0)return [[-.5,0],[0,.5]];if(ori===1)return [[.5,0],[0,.5]];if(ori===2)return [[.5,0],[0,-.5]];return [[-.5,0],[0,-.5]];}if(kind===4){if(ori===0)return [[-.5,0],[0,.5],[0,-.5]];if(ori===1)return [[.5,0],[-.5,0],[0,-.5]];if(ori===2)return [[.5,0],[0,.5],[0,-.5]];return [[.5,0],[-.5,0],[0,.5]];}if(kind===5)return [[-.5,0],[.5,0],[0,-.5],[0,.5]];if(kind===0||kind===1||kind===6||kind===7||kind===8){if(ori===0)return [[.5,0]];if(ori===1)return [[0,.5]];if(ori===2)return [[-.5,0]];return [[0,-.5]];}return [];}");
        sb.Append("function axes(n){if(n.x===1)return [[0,0,-1],[0,1,0],[1,0,0]];if(n.x===-1)return [[0,0,1],[0,1,0],[-1,0,0]];if(n.y===1)return [[1,0,0],[0,0,-1],[0,1,0]];if(n.y===-1)return [[1,0,0],[0,0,1],[0,-1,0]];if(n.z===1)return [[1,0,0],[0,1,0],[0,0,1]];return [[-1,0,0],[0,1,0],[0,0,-1]];}");
        sb.Append("function add3(a,b,s){return [a[0]+b[0]*s,a[1]+b[1]*s,a[2]+b[2]*s];}");
        sb.Append("function wp(c,ep){const ax=axes(c.faceNormal);let p=[c.cubieCoord.x,c.cubieCoord.y,c.cubieCoord.z];p=add3(p,ax[2],0.5);p=add3(p,ax[0],ep[0]);p=add3(p,ax[1],ep[1]);return p;}");
        sb.Append("function key(p){return Math.round(p[0]*2)+','+Math.round(p[1]*2)+','+Math.round(p[2]*2);} ");
        sb.Append("function buildEndpointMap(){const mp={};for(let i=0;i<level.cells.length;i++){const c=level.cells[i];const arr=e2(c.kind,c.orientation);for(let j=0;j<arr.length;j++){const k=key(wp(c,arr[j]));if(!mp[k])mp[k]=[];mp[k].push(i);}}return mp;}");
        sb.Append("function buildPortalPair(){const a={},b={},pair={};for(let i=0;i<level.cells.length;i++){const c=level.cells[i];if(c.kind===7){if(!a[c.portalGroup])a[c.portalGroup]=[];a[c.portalGroup].push(i);}else if(c.kind===8){if(!b[c.portalGroup])b[c.portalGroup]=[];b[c.portalGroup].push(i);}}for(const g in a){if(!b[g])continue;const n=Math.min(a[g].length,b[g].length);for(let i=0;i<n;i++){pair[a[g][i]]=b[g][i];pair[b[g][i]]=a[g][i];}}return pair;}");
        sb.Append("function neighbors(i,epMap,pair){const c=level.cells[i];const arr=e2(c.kind,c.orientation);const out=[];const seen={};for(let j=0;j<arr.length;j++){const li=epMap[key(wp(c,arr[j]))]||[];for(let k=0;k<li.length;k++){const v=li[k];if(v!==i&&!seen[v]){seen[v]=1;out.push(v);}}}if(pair[i]!==undefined&&!seen[pair[i]])out.push(pair[i]);return out;}");
        sb.Append("function recompute(){const n=level.cells.length;const epMap=buildEndpointMap();const pair=buildPortalPair();const filled=new Array(n).fill(false);const q=[];for(let i=0;i<n;i++){const k=level.cells[i].kind;if(k===0||k===6){filled[i]=true;q.push(i);}}for(let h=0;h<q.length;h++){const u=q[h];const nb=neighbors(u,epMap,pair);for(let i=0;i<nb.length;i++){const v=nb[i];if(!filled[v]){filled[v]=true;q.push(v);}}}let endCount=0,endWithWater=0;for(let i=0;i<n;i++){if(level.cells[i].kind===1){endCount++;if(filled[i])endWithWater++;}}return {filled,solved:endCount>0&&endWithWater===endCount,endCount,endWithWater};}");
        sb.Append("function faceUV(face,c){const x=c.cubieCoord.x,y=c.cubieCoord.y,z=c.cubieCoord.z;if(face==='+X')return {u:-z,v:y};if(face==='-X')return {u:z,v:y};if(face==='+Y')return {u:x,v:-z};if(face==='-Y')return {u:x,v:z};if(face==='+Z')return {u:x,v:y};return {u:-x,v:y};}");
        sb.Append("function render(){const s=recompute();const state=document.getElementById('state');state.textContent=s.solved?'已通关':'未通关';state.className='pill '+(s.solved?'ok':'bad');document.getElementById('endStat').textContent='终点 '+s.endWithWater+'/'+s.endCount;document.getElementById('moveStat').textContent='步数 '+moves;const win=document.getElementById('win');if(s.solved&&!wasSolved){win.classList.add('show');setTimeout(()=>win.classList.remove('show'),1200);}wasSolved=s.solved;const root=document.getElementById('root');root.innerHTML='';faces.forEach(face=>{const box=document.createElement('div');box.className='face';box.innerHTML='<h3>'+face+'</h3>';const arr=[];for(let i=0;i<level.cells.length;i++){if(fName(level.cells[i].faceNormal)===face)arr.push({idx:i,c:level.cells[i]});}if(arr.length===0){const empty=document.createElement('div');empty.textContent='此面无管道';empty.style.opacity='.72';box.appendChild(empty);root.appendChild(box);return;}let uMin=999,uMax=-999,vMin=999,vMax=-999;arr.forEach(it=>{const uv=faceUV(face,it.c);it.u=uv.u;it.v=uv.v;if(uv.u<uMin)uMin=uv.u;if(uv.u>uMax)uMax=uv.u;if(uv.v<vMin)vMin=uv.v;if(uv.v>vMax)vMax=uv.v;});const cols=uMax-uMin+1,rows=vMax-vMin+1;const board=document.createElement('div');board.className='board';board.style.gridTemplateColumns='repeat('+cols+',minmax(66px,1fr))';for(let r=0;r<rows;r++){for(let c=0;c<cols;c++){const tile=document.createElement('div');tile.className='tile empty';board.appendChild(tile);}}arr.forEach(it=>{const col=it.u-uMin;const row=vMax-it.v;const index=row*cols+col;const tile=board.children[index];tile.className='tile'+(s.filled[it.idx]?' on':'');const canRot=rotMod(it.c.kind)>1;const tail=(it.c.kind===7||it.c.kind===8)?(' G'+it.c.portalGroup):'';tile.innerHTML='<div><div class=k>'+kShort(it.c.kind)+' '+kName(it.c.kind)+tail+'</div><div class=meta>坐标 ('+it.c.cubieCoord.x+','+it.c.cubieCoord.y+','+it.c.cubieCoord.z+')</div></div><div class=rot><span class=dir>'+oriChar(it.c.orientation)+'</span><button '+(canRot?'':'disabled')+'>旋转</button></div>';const btn=tile.querySelector('button');if(canRot){btn.onclick=()=>{it.c.orientation=(it.c.orientation+1)%rotMod(it.c.kind);moves++;render();};}});box.appendChild(board);root.appendChild(box);});}");
        sb.Append("render();</script></body></html>");
        return sb.ToString();
    }

    string EscapeHtml(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
    }

    void OnLoadMyLevelClicked()
    {
        if (puzzle == null) return;
        string id = (levelIdField != null && !string.IsNullOrEmpty(levelIdField.text)) ? levelIdField.text.Trim() : "";
        if (string.IsNullOrEmpty(id))
        {
            SetStatus("请先输入我的关卡ID，或点击“我的关卡”查看列表");
            return;
        }

        var data = PlayerLevelStore.Load(id);
        if (data == null)
        {
            SetStatus("我的关卡中未找到：" + id);
            return;
        }

        puzzle.LoadFromData(data, true);
        if (levelNameField != null) levelNameField.text = data.displayName;
        if (moveLimitField != null) moveLimitField.text = data.moveLimit.ToString();
        SetStatus("已从我的关卡读取：" + id + "（" + data.cells.Count + " 格）");
    }

    void OnMyLevelsClicked()
    {
        EnsureMyLevelsPanel();
        if (_myLevelsPanel == null) return;
        _myLevelsPanel.SetActive(true);
        RefreshMyLevelsPanel();
    }

    Button EnsureMyLevelsButton()
    {
        var existing = GameObject.Find("我的关卡");
        if (existing != null)
        {
            var b = existing.GetComponent<Button>();
            if (b != null) return b;
        }
        if (loadButton == null) return null;

        var clone = Instantiate(loadButton.gameObject, loadButton.transform.parent);
        clone.name = "我的关卡";
        var rt = clone.GetComponent<RectTransform>();
        var baseRt = loadButton.GetComponent<RectTransform>();
        if (rt != null && baseRt != null)
            rt.anchoredPosition = baseRt.anchoredPosition + new Vector2(0f, -70f);

        var b2 = clone.GetComponent<Button>();
        if (b2 == null) return null;
        b2.onClick.RemoveAllListeners();
        SetButtonLabel(b2, "我的关卡");
        return b2;
    }

    string GenerateDefaultId()
    {
        var ids = PlayerLevelStore.LoadIds();
        int idx = ids.Count + 1;
        string id = "my_level_" + idx;
        while (ids.Contains(id))
        {
            idx++;
            id = "my_level_" + idx;
        }
        return id;
    }

    void SetButtonLabel(Button btn, string label)
    {
        if (btn == null) return;
        var txt = btn.GetComponentInChildren<Text>();
        if (txt != null) txt.text = label;
    }

    void SetStatus(string s)
    {
        if (statusText != null) statusText.text = s;
    }

    void EnsureMyLevelsPanel()
    {
        if (_myLevelsPanel != null) return;
        var canvas = GetComponentInChildren<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var panel = new GameObject("MyLevelsPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(840f, 620f);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.14f, 0.95f);

        var title = CreateText(panel.transform, "Title", "我的关卡", 34, TextAnchor.MiddleCenter,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(-140f, 44f));
        if (title != null) title.color = new Color(1f, 0.9f, 0.45f, 1f);

        var close = CreateButton(panel.transform, "CloseBtn", "关闭",
            new Color(0.42f, 0.30f, 0.30f),
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(110f, 44f));
        if (close != null)
            close.onClick.AddListener(() => panel.SetActive(false));

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(panel.transform, false);
        var viewportRt = viewport.GetComponent<RectTransform>();
        viewportRt.anchorMin = new Vector2(0f, 0f);
        viewportRt.anchorMax = new Vector2(1f, 1f);
        viewportRt.pivot = new Vector2(0.5f, 0.5f);
        viewportRt.anchoredPosition = new Vector2(0f, -20f);
        viewportRt.sizeDelta = new Vector2(-40f, -120f);
        var viewportImg = viewport.GetComponent<Image>();
        viewportImg.color = new Color(1f, 1f, 1f, 0.02f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 0f);

        var scroll = panel.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.viewport = viewportRt;
        scroll.content = contentRt;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32f;

        _myLevelsPanel = panel;
        _myLevelsContent = contentRt;
        _myLevelsScroll = scroll;
        _myLevelsPanel.SetActive(false);
    }

    void RefreshMyLevelsPanel()
    {
        if (_myLevelsContent == null) return;

        for (int i = _myLevelsContent.childCount - 1; i >= 0; i--)
            Destroy(_myLevelsContent.GetChild(i).gameObject);

        var levels = PlayerLevelStore.LoadLevels();
        if (levels.Count == 0)
        {
            CreateText(_myLevelsContent, "Empty", "还没有保存任何玩家关卡", 24, TextAnchor.MiddleCenter,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return;
        }

        float rowH = 62f;
        for (int i = 0; i < levels.Count; i++)
        {
            var lv = levels[i];
            if (lv == null || string.IsNullOrEmpty(lv.id)) continue;

            var row = new GameObject("Row_" + lv.id, typeof(RectTransform), typeof(Image));
            row.transform.SetParent(_myLevelsContent, false);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -i * rowH);
            rowRt.sizeDelta = new Vector2(0f, rowH - 6f);
            row.GetComponent<Image>().color = (i % 2 == 0)
                ? new Color(1f, 1f, 1f, 0.06f)
                : new Color(1f, 1f, 1f, 0.03f);

            string name = string.IsNullOrEmpty(lv.displayName) ? lv.id : lv.displayName;
            string info = lv.id + "  |  " + name + "  |  格子:" + (lv.cells != null ? lv.cells.Count : 0);
            CreateText(row.transform, "Info", info, 20, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(-280f, 0f));

            string capturedId = lv.id;
            var shareBtn = CreateButton(row.transform, "Share", "分享", new Color(0.78f, 0.53f, 0.20f),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-236f, 0f), new Vector2(90f, 40f));
            if (shareBtn != null)
                shareBtn.onClick.AddListener(() => ShowShareLinkPopup(capturedId));

            var loadBtn = CreateButton(row.transform, "Load", "载入", new Color(0.20f, 0.55f, 0.80f),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-130f, 0f), new Vector2(90f, 40f));
            if (loadBtn != null)
                loadBtn.onClick.AddListener(() => LoadMyLevelById(capturedId));

            var delBtn = CreateButton(row.transform, "Delete", "删除", new Color(0.70f, 0.28f, 0.28f),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(90f, 40f));
            if (delBtn != null)
                delBtn.onClick.AddListener(() => DeleteMyLevelById(capturedId));
        }

        float totalHeight = levels.Count * rowH + 8f;
        _myLevelsContent.sizeDelta = new Vector2(0f, totalHeight);
        if (_myLevelsScroll != null) _myLevelsScroll.verticalNormalizedPosition = 1f;
    }

    void LoadMyLevelById(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (levelIdField != null) levelIdField.text = id;
        OnLoadMyLevelClicked();
        if (_myLevelsPanel != null) _myLevelsPanel.SetActive(false);
    }

    void DeleteMyLevelById(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (PlayerLevelStore.Delete(id))
        {
            SetStatus("已删除我的关卡：" + id);
            if (levelIdField != null && levelIdField.text == id)
                levelIdField.text = "";
        }
        else
        {
            SetStatus("删除失败：" + id);
        }
        RefreshMyLevelsPanel();
    }

    Text CreateText(Transform parent, string name, string txt, int fontSize, TextAnchor align,
        Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 anchored, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchored;
        rt.sizeDelta = size;
        var t = go.GetComponent<Text>();
        t.text = txt;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = new Color(1f, 1f, 1f, 0.92f);
        return t;
    }

    Button CreateButton(Transform parent, string name, string label, Color color,
        Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 anchored, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchored;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = color;

        var btn = go.GetComponent<Button>();
        var txt = CreateText(go.transform, "Label", label, 20, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        if (txt != null) txt.color = Color.white;
        return btn;
    }
}
