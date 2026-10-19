using System.Collections.Generic;
using UnityEngine;

/// <summary>Supported UI languages (5 mainstream options).</summary>
public enum CassetteLanguage
{
    ChineseSimplified = 0,
    English = 1,
    Japanese = 2,
    Korean = 3,
    Spanish = 4
}

/// <summary>
/// Lightweight runtime localisation for the whole game UI.
///   • 5 languages: 简体中文 / English / 日本語 / 한국어 / Español
///   • the chosen language is persisted in PlayerPrefs and restored on the next launch
///   • on first launch the OS language is used when it matches one of the five
/// Usage: CassetteLocalization.T("hud.hpLabel", hp, maxHp)
/// </summary>
public static class CassetteLocalization
{
    public const string PrefKey = "CassetteDash.Language";

    public static readonly CassetteLanguage[] AllLanguages =
    {
        CassetteLanguage.ChineseSimplified,
        CassetteLanguage.English,
        CassetteLanguage.Japanese,
        CassetteLanguage.Korean,
        CassetteLanguage.Spanish
    };

    static CassetteLanguage _current = CassetteLanguage.ChineseSimplified;
    static bool _loaded;

    /// <summary>Raised after the language changed, so open UI can refresh immediately.</summary>
    public static event System.Action LanguageChanged;

    public static CassetteLanguage Current
    {
        get { EnsureLoaded(); return _current; }
        set
        {
            EnsureLoaded();
            if (_current == value) return;
            _current = value;
            PlayerPrefs.SetString(PrefKey, value.ToString());
            PlayerPrefs.Save();
            if (LanguageChanged != null) LanguageChanged();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        EnsureLoaded();
    }

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        string saved = PlayerPrefs.GetString(PrefKey, "");
        if (!string.IsNullOrEmpty(saved))
        {
            try
            {
                _current = (CassetteLanguage)System.Enum.Parse(typeof(CassetteLanguage), saved);
                return;
            }
            catch { /* fall through to OS detection */ }
        }

        _current = FromSystemLanguage(Application.systemLanguage);
    }

    static CassetteLanguage FromSystemLanguage(SystemLanguage sys)
    {
        switch (sys)
        {
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional:
                return CassetteLanguage.ChineseSimplified;
            case SystemLanguage.Japanese:
                return CassetteLanguage.Japanese;
            case SystemLanguage.Korean:
                return CassetteLanguage.Korean;
            case SystemLanguage.Spanish:
                return CassetteLanguage.Spanish;
            default:
                return CassetteLanguage.English;
        }
    }

    /// <summary>Native display name shown on the language buttons.</summary>
    public static string NativeName(CassetteLanguage lang)
    {
        switch (lang)
        {
            case CassetteLanguage.ChineseSimplified: return "简体中文";
            case CassetteLanguage.Japanese: return "日本語";
            case CassetteLanguage.Korean: return "한국어";
            case CassetteLanguage.Spanish: return "Español";
            default: return "English";
        }
    }

    public static string CurrentNativeName => NativeName(Current);

    // ==========================================================================================
    // String table
    // ==========================================================================================

    static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>();

    static void Add(string key, string zh, string en, string ja, string ko, string es)
    {
        Table[key] = new[] { zh, en, ja, ko, es };
    }

    static CassetteLocalization()
    {
        // ---- Start / title screen ----
        Add("start.subtitle",
            "磁带跑酷  ·  逃出磁带内部，登上列车",
            "Cassette parkour  ·  Escape the tape and board the train",
            "カセットパルクール  ·  テープの外へ、列車に乗り込め",
            "카세트 파쿠르  ·  테이프를 탈출해 열차에 타라",
            "Parkour en casete  ·  Escapa de la cinta y sube al tren");

        Add("start.pressAnyKey",
            "按任意键开始",
            "PRESS ANY KEY TO START",
            "任意のキーでスタート",
            "아무 키나 눌러 시작",
            "PULSA CUALQUIER TECLA PARA EMPEZAR");

        Add("start.controls",
            "A / D 移动    ·    Space 跳跃    ·    E / Q 吐带    ·    F 交互 / 上梯 / 上车    ·    R 重生",
            "A / D Move    ·    Space Jump    ·    E / Q Feed tape    ·    F Interact / Ladder / Train    ·    R Respawn",
            "A / D 移動    ·    Space ジャンプ    ·    E / Q テープ送り    ·    F 操作 / はしご / 列車    ·    R リスポーン",
            "A / D 이동    ·    Space 점프    ·    E / Q 테이프 풀기    ·    F 상호작용 / 사다리 / 열차    ·    R 리스폰",
            "A / D Mover    ·    Space Saltar    ·    E / Q Soltar cinta    ·    F Interactuar / Escalera / Tren    ·    R Reaparecer");

        Add("start.language",
            "语言",
            "Language",
            "言語",
            "언어",
            "Idioma");

        Add("start.languageHint",
            "点击按钮或按数字键 1-5 切换语言",
            "Click a button or press 1-5 to change the language",
            "ボタンをクリック、または 1-5 キーで言語を変更",
            "버튼을 클릭하거나 1-5 키로 언어를 변경",
            "Haz clic en un botón o pulsa 1-5 para cambiar el idioma");

        // ---- In-game HUD ----
        Add("hud.controls",
            "操作：A/D 左右 | ←/→ 里外 | Space 跳跃 | Ctrl 蹲下 | 长按 E 凹陷左带 | 长按 Q 凹陷右带并升降 | R 重生",
            "Controls: A/D Left-Right | ←/→ Depth | Space Jump | Ctrl Crouch | Hold E Sag left tape | Hold Q Sag right tape & lift | R Respawn",
            "操作：A/D 左右 | ←/→ 奥行き | Space ジャンプ | Ctrl しゃがむ | E 長押しで左テープをたるませる | Q 長押しで右テープをたるませて上昇 | R リスポーン",
            "조작: A/D 좌우 | ←/→ 깊이 | Space 점프 | Ctrl 앉기 | E 길게 눌러 좌측 테이프 늘어뜨리기 | Q 길게 눌러 우측 테이프 늘어뜨리고 상승 | R 리스폰",
            "Controles: A/D Izquierda-Derecha | ←/→ Profundidad | Space Saltar | Ctrl Agacharse | Mantén E para hundir la cinta izquierda | Mantén Q para hundir la derecha y elevarte | R Reaparecer");

        Add("hud.dead",
            "【生命值归零倒地！等待动画播放完毕自动重生，或按 R 立即重生】",
            "[Down! Wait for the animation to finish to auto-respawn, or press R to respawn now]",
            "【HP が 0 になり倒れました！アニメ終了後に自動リスポーン、または R で即リスポーン】",
            "【체력이 0이 되어 쓰러졌습니다! 애니메이션이 끝나면 자동 리스폰, R 키로 즉시 리스폰】",
            "[¡Caído! Espera a que termine la animación para reaparecer, o pulsa R para reaparecer ya]");

        Add("hud.feedRight",
            "【长按 Q 顺时针旋转右齿轮吐带！跳入右侧带子后松开 Q，带子绷紧将你顺势带上右齿轮！】",
            "[Hold Q to spin the right reel and feed tape! Jump into the right strip, then release Q to be carried up onto the right reel!]",
            "【Q 長押しで右リールを回してテープを送る！右のテープに飛び乗り、Q を離すとテープが張って右リールへ運ばれる！】",
            "【Q를 길게 눌러 오른쪽 릴을 돌려 테이프를 풀어라! 오른쪽 테이프로 뛰어든 뒤 Q를 놓으면 테이프가 팽팽해지며 오른쪽 릴로 올려준다!】",
            "[¡Mantén Q para girar el carrete derecho y soltar cinta! Salta a la cinta derecha y suelta Q para que te lleve al carrete.]");

        Add("hud.feedLeft",
            "【长按 E 旋转左齿轮吐带！左侧带子凹陷成吊床缓冲】",
            "[Hold E to spin the left reel and feed tape! The left strip sags into a soft hammock]",
            "【E 長押しで左リールを回してテープを送る！左のテープがたわんでハンモック状のクッションになる】",
            "【E를 길게 눌러 왼쪽 릴을 돌려 테이프를 풀어라! 왼쪽 테이프가 처져 해먹처럼 완충된다】",
            "[¡Mantén E para girar el carrete izquierdo y soltar cinta! La cinta izquierda se hunde como una hamaca]");

        Add("hud.tips",
            "操作技巧：长按 Q 凹下右带，跳入后松开 Q 自动弹上右齿轮！长按 E 凹陷左带缓冲。",
            "Tip: hold Q to sag the right strip, jump in, then release Q to ride up to the right reel. Hold E to sag the left strip as a cushion.",
            "コツ：Q 長押しで右テープをたるませ、飛び込んでから Q を離すと右リールへ上がれる。E 長押しで左テープをたるませてクッションに。",
            "팁: Q를 길게 눌러 오른쪽 테이프를 늘어뜨리고 뛰어든 뒤 Q를 놓으면 오른쪽 릴로 올라간다. E를 길게 눌러 왼쪽 테이프를 완충용으로 늘어뜨려라.",
            "Consejo: mantén Q para hundir la cinta derecha, salta dentro y suelta Q para subir al carrete. Mantén E para hundir la izquierda como colchón.");

        Add("hud.slack",
            "左侧松弛度 (E): {0} / {1} | 右侧松弛度 (Q): {2} / {3} | 景深 Z: {4}",
            "Left slack (E): {0} / {1} | Right slack (Q): {2} / {3} | Depth Z: {4}",
            "左テープのたるみ (E): {0} / {1} | 右テープのたるみ (Q): {2} / {3} | 奥行き Z: {4}",
            "왼쪽 느슨함 (E): {0} / {1} | 오른쪽 느슨함 (Q): {2} / {3} | 깊이 Z: {4}",
            "Holgura izquierda (E): {0} / {1} | Holgura derecha (Q): {2} / {3} | Profundidad Z: {4}");

        Add("hud.hpLabel",
            "生命值 HP: {0} / {1}",
            "Health HP: {0} / {1}",
            "体力 HP: {0} / {1}",
            "체력 HP: {0} / {1}",
            "Salud HP: {0} / {1}");

        Add("hud.hpDownWait",
            "HP: 0 / {0}  【已倒地！{1:F1}s 后自动重生 (按 R 立即重生)】",
            "HP: 0 / {0}  [Down! Auto-respawn in {1:F1}s (press R to respawn now)]",
            "HP: 0 / {0}  【倒れました！{1:F1} 秒後に自動リスポーン（R で即リスポーン）】",
            "HP: 0 / {0}  【쓰러짐! {1:F1}초 후 자동 리스폰 (R로 즉시 리스폰)】",
            "HP: 0 / {0}  [¡Caído! Reaparece en {1:F1}s (pulsa R para reaparecer ya)]");

        Add("hud.hpDownFalling",
            "HP: 0 / {0}  【已摔空！正在受击倒地... (按 R 立即重生)】",
            "HP: 0 / {0}  [Out of health! Knocked down... (press R to respawn now)]",
            "HP: 0 / {0}  【HP 尽きました！倒れています...（R で即リスポーン）】",
            "HP: 0 / {0}  【체력 소진! 쓰러지는 중... (R로 즉시 리스폰)】",
            "HP: 0 / {0}  [¡Sin salud! Cayendo... (pulsa R para reaparecer ya)]");

        Add("hud.damageLethal",
            "-{0} HP 直接摔没血！",
            "-{0} HP  fatal fall!",
            "-{0} HP  落下で即死！",
            "-{0} HP  낙하로 즉사!",
            "-{0} HP  ¡caída mortal!");

        Add("hud.damage",
            "-{0} HP 受到伤害！",
            "-{0} HP  damage taken!",
            "-{0} HP  ダメージ！",
            "-{0} HP  피해!",
            "-{0} HP  ¡daño recibido!");

        Add("hud.victory",
            "🚂 火车已启动！鸣笛出发，前往下一磁带世界！ 🚂",
            "🚂 The train is departing! All aboard to the next cassette world! 🚂",
            "🚂 列車が出発！次のカセット世界へ！ 🚂",
            "🚂 열차 출발! 다음 카세트 세계로! 🚂",
            "🚂 ¡El tren parte hacia el siguiente mundo de casete! 🚂");

        Add("hud.ladderClimb",
            "【梯子攀爬中】 W 向上爬 | S 向下爬 | Space / F 离开梯子",
            "[Climbing ladder] W Up | S Down | Space / F Leave ladder",
            "【はしご登り中】 W 上へ | S 下へ | Space / F はしごを離れる",
            "【사다리 등반 중】 W 위로 | S 아래로 | Space / F 사다리에서 내리기",
            "[Subiendo la escalera] W Subir | S Bajar | Space / F Soltarse");

        Add("hud.pressTrain",
            "【 按 F 启动并登上火车 】",
            "[ Press F to start and board the train ]",
            "【 F で列車を起動して乗車 】",
            "【 F를 눌러 열차에 탑승 】",
            "[ Pulsa F para arrancar y subir al tren ]");

        Add("hud.pressLadder",
            "【 按 F 爬上梯子 】",
            "[ Press F to climb the ladder ]",
            "【 F ではしごを登る 】",
            "【 F를 눌러 사다리 오르기 】",
            "[ Pulsa F para subir la escalera ]");

        // ---- Ending / game over screen ----
        Add("end.title",
            "游戏结束",
            "GAME OVER",
            "ゲームオーバー",
            "게임 오버",
            "FIN DEL JUEGO");

        Add("end.subtitle",
            "STAGE CLEAR  ·  磁带列车已驶向远方",
            "STAGE CLEAR  ·  The cassette train has departed",
            "STAGE CLEAR  ·  カセット列車は遠くへ",
            "STAGE CLEAR  ·  카세트 열차가 떠났습니다",
            "STAGE CLEAR  ·  El tren de casete se ha marchado");

        Add("end.stats",
            "通关用时  {0}        剩余生命  {1}",
            "Clear time  {0}        Health left  {1}",
            "クリアタイム  {0}        残り体力  {1}",
            "클리어 시간  {0}        남은 체력  {1}",
            "Tiempo  {0}        Salud restante  {1}");

        Add("end.restart",
            "按  R / 空格  重新开始",
            "Press R / Space to restart",
            "R / Space でリスタート",
            "R / Space 로 다시 시작",
            "Pulsa R / Space para reiniciar");
    }

    /// <summary>Localised string by key (falls back to English, then to the key itself).</summary>
    public static string T(string key)
    {
        EnsureLoaded();
        string[] row;
        if (!Table.TryGetValue(key, out row) || row == null) return key;
        int idx = (int)_current;
        if (idx < 0 || idx >= row.Length) idx = (int)CassetteLanguage.English;
        string s = row[idx];
        if (string.IsNullOrEmpty(s)) s = row[(int)CassetteLanguage.English];
        return string.IsNullOrEmpty(s) ? key : s;
    }

    /// <summary>Localised string with string.Format arguments.</summary>
    public static string T(string key, params object[] args)
    {
        string fmt = T(key);
        if (args == null || args.Length == 0) return fmt;
        try { return string.Format(fmt, args); }
        catch { return fmt; }
    }
}