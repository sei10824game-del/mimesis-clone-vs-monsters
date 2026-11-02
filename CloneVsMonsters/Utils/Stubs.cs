#if STUBS || SKIP_GAME_REFS
// このファイルはゲーム本体がない環境（CI / レビュー環境）でビルドを通すための最小スタブです。
// 実際のゲーム環境では GamePath が指定され、このファイルは除外されます。
// CIでは dotnet build -p:SkipGameRefs=true でビルドしてください。

using System;

namespace Mimic
{
    public class Hub { public static Hub? s; public object? pdata; }
    public class VWorld { }
    public class VRoomManager { }
    public class VActor { }
    public class StatManager
    {
        public long GetCurrentStamina() => 0;
        public void RegenerateStamina(long delta) { }
        public void SetMutableStat(object type, long value) { }
        public object? GetStats() => null;
    }
    public enum MutableStatType { Stamina }
    public class MovementController { }
    public class InventoryItem { }
}

namespace Mimic.Actors
{
    public class ProtoActor : UnityEngine.MonoBehaviour
    {
        public uint ActorID;
        public ulong UID;
        public bool dead;
        public bool AmIAvatar() => false;
        public Mimic.Hub.PersistentData? main;
    }
    public class VMonster : Mimic.VActor { }
    public class VPlayer : Mimic.VActor { }
    public class VMimic : Mimic.VActor { }
    public class VActor { }
}

namespace ReluProtocol { }

// MelonLoader スタブは実際に MelonLoader.dll がない場合のみ
// 注意: 実際のビルドでは MelonLoader.dll が参照されるため、このスタブは使われません。
#endif
