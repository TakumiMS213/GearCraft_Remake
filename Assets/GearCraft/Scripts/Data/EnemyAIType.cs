public enum EnemyAIType
{
    Straight,       // 直進してゲートに向かう
    CircleMove,     // 円軌道で動きつつ射撃
    Sniper,         // 遠距離から精密射撃
    Charger,        // 突進＆体当たり
    Bomber,         // 自爆型
    Boss_Tank,      // ボス1(Stage10): 高HP・広範囲弾幕
    Boss_Speed,     // ボス2(Stage20): 高速移動・突進+射撃
    Boss_Artillery, // ボス3(Stage30): 遠距離ミサイル・爆撃
    Boss_Final      // ラスボス(Stage31): 全パターン複合
}
