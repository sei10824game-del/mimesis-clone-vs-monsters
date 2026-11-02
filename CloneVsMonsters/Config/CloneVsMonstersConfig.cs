using MelonLoader;

namespace CloneVsMonsters.Config
{
    /// <summary>
    /// MOD設定。MelonPreferences で永続化され、ゲーム起動中でも即時反映される。
    /// </summary>
    internal static class CloneVsMonstersConfig
    {
        private const string CategoryId = "CloneVsMonsters";

        private static MelonPreferences_Category? _category;

        // 各設定エントリ
        private static MelonPreferences_Entry<bool>? _enabled;
        private static MelonPreferences_Entry<bool>? _preventFakeAttackPlayer;
        private static MelonPreferences_Entry<bool>? _preventFakeAttackFake;
        private static MelonPreferences_Entry<bool>? _preventPlayerAttackFake;
        private static MelonPreferences_Entry<bool>? _makeFakeAttackMonster;
        private static MelonPreferences_Entry<bool>? _makeMonsterAttackFake;
        private static MelonPreferences_Entry<bool>? _verboseLog;

        // 公開プロパティ（パッチから参照）
        public static bool Enabled => _enabled?.Value ?? true;
        public static bool PreventFakeAttackPlayer => _preventFakeAttackPlayer?.Value ?? true;
        public static bool PreventFakeAttackFake => _preventFakeAttackFake?.Value ?? true;
        public static bool PreventPlayerAttackFake => _preventPlayerAttackFake?.Value ?? true;
        public static bool MakeFakeAttackMonster => _makeFakeAttackMonster?.Value ?? true;
        public static bool MakeMonsterAttackFake => _makeMonsterAttackFake?.Value ?? false;
        public static bool VerboseLog => _verboseLog?.Value ?? false;

        public static void Initialize()
        {
            if (_category != null) return;

            _category = MelonPreferences.CreateCategory(CategoryId, "CloneVsMonsters - 偽物 vs モンスター");

            _enabled = CreateEntry("Enabled", true,
                "有効",
                "MOD全体の有効/無効。無効にすると全パッチがスキップされバニラ挙動に戻ります。");

            _preventFakeAttackPlayer = CreateEntry("PreventFakeAttackPlayer", true,
                "偽物→プレイヤー攻撃を無効化",
                "偽物がプレイヤーにダメージを与えるのを防ぎます。");

            _preventFakeAttackFake = CreateEntry("PreventFakeAttackFake", true,
                "偽物→偽物攻撃を無効化",
                "偽物同士が攻撃し合うのを防ぎます。");

            _preventPlayerAttackFake = CreateEntry("PreventPlayerAttackFake", true,
                "プレイヤー→偽物攻撃を無効化",
                "プレイヤーが偽物を誤爆してもダメージが入りません。共闘をスムーズにします。");

            _makeFakeAttackMonster = CreateEntry("MakeFakeAttackMonster", true,
                "偽物→モンスター攻撃を有効化",
                "偽物がモンスターを優先的に攻撃するようにします。");

            _makeMonsterAttackFake = CreateEntry("MakeMonsterAttackFake", false,
                "モンスター→偽物攻撃を有効化（囮）",
                "モンスターが偽物を襲うようにします。有効にすると偽物が囮として機能します。無効でも偽物側からは攻撃します。");

            _verboseLog = CreateEntry("VerboseLog", false,
                "詳細ログ",
                "ダメージやターゲット変更の詳細を MelonLoader/Latest.log に出力します。デバッグ用。");

            // 既存の cfg を読み込み、なければデフォルトで保存
            _category.SaveToFile(false);

            MelonLogger.Msg($"[CloneVsMonsters] Config loaded: Enabled={Enabled}, PreventFake->Player={PreventFakeAttackPlayer}, PreventFake->Fake={PreventFakeAttackFake}, PreventPlayer->Fake={PreventPlayerAttackFake}, Fake->Monster={MakeFakeAttackMonster}, Monster->Fake={MakeMonsterAttackFake}");
        }

        private static MelonPreferences_Entry<T> CreateEntry<T>(string identifier, T defaultValue, string displayName, string description)
        {
            if (_category == null)
                throw new InvalidOperationException("Category not initialized");
            return _category.CreateEntry(identifier, defaultValue, displayName, description);
        }

        /// <summary>
        /// 設定変更を即時保存（ゲーム内UIから呼ばれる想定）
        /// </summary>
        public static void Save()
        {
            _category?.SaveToFile(false);
        }
    }
}
