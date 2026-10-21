#!/usr/bin/env python3
"""Extract and exercise the production AR stun callbacks; create executables only in /private/tmp."""
from datetime import datetime
import hashlib
from pathlib import Path
import subprocess
import tempfile

PROJECT = Path(__file__).resolve().parents[2]
SOURCE = PROJECT / "Assets/GearCraft/Scripts/EnemyAI/EnemyAI_BossSpeed.cs"
REPORT = Path(__file__).with_name("ar-stun-callback-tests.txt")
MONO = "/Applications/Unity/Hub/Editor/6000.0.67f1/Unity.app/Contents/MonoBleedingEdge/bin/mono"
MCS = "/Applications/Unity/Hub/Editor/6000.0.67f1/Unity.app/Contents/MonoBleedingEdge/lib/mono/4.5/mcs.exe"
source_text = SOURCE.read_text()


def extract_method(signature):
    start = source_text.index(signature)
    opening = source_text.index("{", start)
    depth = 0
    for position in range(opening, len(source_text)):
        if source_text[position] == "{":
            depth += 1
        elif source_text[position] == "}":
            depth -= 1
            if depth == 0:
                return source_text[start:position + 1]
    raise ValueError("Unclosed method: " + signature)


methods = "\n".join(extract_method(signature) for signature in [
    "public void SetStunned(bool stunned)",
    "private void StopTweens()",
    "private void OnDisable()",
    "public void OnDeath()",
])
code = r'''
using System;

// DOTween の API 呼出しを記録するテストダブル。実エンジンでの時間進行は検証対象外。
public class Tween {
    public bool active=true, playing=true;
    public float timeScale=0.5f;
    public int playCalls, pauseCalls, killCalls;
    public bool IsActive(){ return active; }
    public bool IsPlaying(){ return playing; }
    public void Play(){ playing=true; playCalls++; }
    public void Pause(){ playing=false; pauseCalls++; }
    public void Kill(){ active=false; playing=false; killCalls++; }
}
public class Sequence : Tween {}
public class Owner { public float HP=100; public bool isActiveAndEnabled=true; }
public class ExtractedBossSpeed {
    public Owner owner=new Owner();
    public Tween currentTween=new Tween();
    public Sequence attackSequence=new Sequence();
    private bool resumeMovementAfterStun, resumeAttackAfterStun, stunPaused;
    public void Disable(){ OnDisable(); }
''' + methods + r'''
}
public static class Tests {
    static int passed, failed;
    static void Check(bool value,string detail){ if(!value)throw new Exception(detail); }
    static void Test(string name,Action run){
        try{run();passed++;Console.WriteLine("PASS "+name);}
        catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}
    }
    public static int Main(){
        Test("進行中の移動・連射を両方停止して同じTweenを再開",()=>{
            var ai=new ExtractedBossSpeed();var movement=ai.currentTween;var attack=ai.attackSequence;
            ai.SetStunned(true);Check(!movement.playing&&!attack.playing,"pause both");
            ai.SetStunned(false);Check(movement.playing&&attack.playing,"resume both");
            Check(ai.currentTween==movement&&ai.attackSequence==attack,"preserve instances");
        });
        Test("スタン延長の重複通知でも元の再開状態を失わない",()=>{
            var ai=new ExtractedBossSpeed();ai.SetStunned(true);ai.SetStunned(true);
            Check(ai.currentTween.pauseCalls==1&&ai.attackSequence.pauseCalls==1,"pause once");
            ai.SetStunned(false);Check(ai.currentTween.playCalls==1&&ai.attackSequence.playCalls==1,"resume once");
        });
        Test("復帰の重複通知を無視",()=>{
            var ai=new ExtractedBossSpeed();ai.SetStunned(true);ai.SetStunned(false);ai.SetStunned(false);
            Check(ai.currentTween.playCalls==1&&ai.attackSequence.playCalls==1,"single resume");
        });
        Test("スタン前から停止中のTweenは勝手に再開しない",()=>{
            var ai=new ExtractedBossSpeed();ai.currentTween.playing=false;ai.attackSequence.playing=false;
            ai.SetStunned(true);ai.SetStunned(false);
            Check(!ai.currentTween.playing&&!ai.attackSequence.playing,"still paused");
            Check(ai.currentTween.playCalls==0&&ai.attackSequence.playCalls==0,"no resume call");
        });
        Test("移動だけ進行中なら移動だけ再開",()=>{
            var ai=new ExtractedBossSpeed();ai.attackSequence.playing=false;
            ai.SetStunned(true);ai.SetStunned(false);
            Check(ai.currentTween.playing&&!ai.attackSequence.playing,"mixed state");
        });
        Test("連射だけ進行中なら連射だけ再開",()=>{
            var ai=new ExtractedBossSpeed();ai.currentTween.playing=false;
            ai.SetStunned(true);ai.SetStunned(false);
            Check(!ai.currentTween.playing&&ai.attackSequence.playing,"mixed state");
        });
        Test("Tween未作成時の停止・復帰は安全",()=>{
            var ai=new ExtractedBossSpeed();ai.currentTween=null;ai.attackSequence=null;
            ai.SetStunned(true);ai.SetStunned(false);
        });
        Test("死亡後は停止していた攻撃も移動も再開しない",()=>{
            var ai=new ExtractedBossSpeed();ai.SetStunned(true);ai.owner.HP=0;ai.SetStunned(false);
            Check(ai.currentTween.playCalls==0&&ai.attackSequence.playCalls==0,"dead owner");
        });
        Test("無効化された個体のTweenを再開しない",()=>{
            var ai=new ExtractedBossSpeed();ai.SetStunned(true);ai.owner.isActiveAndEnabled=false;ai.SetStunned(false);
            Check(ai.currentTween.playCalls==0&&ai.attackSequence.playCalls==0,"disabled owner");
        });
        Test("ownerが消えても再開せず安全",()=>{
            var ai=new ExtractedBossSpeed();ai.SetStunned(true);ai.owner=null;ai.SetStunned(false);
            Check(ai.currentTween.playCalls==0&&ai.attackSequence.playCalls==0,"missing owner");
        });
        Test("スタン中に破棄されたTweenを再開しない",()=>{
            var ai=new ExtractedBossSpeed();ai.SetStunned(true);ai.currentTween.Kill();ai.attackSequence.Kill();ai.SetStunned(false);
            Check(ai.currentTween.playCalls==0&&ai.attackSequence.playCalls==0,"killed tweens");
        });
        Test("OnDisableでTween参照と停止履歴を破棄",()=>{
            var ai=new ExtractedBossSpeed();var movement=ai.currentTween;var attack=ai.attackSequence;
            ai.SetStunned(true);ai.Disable();
            Check(!movement.active&&!attack.active,"killed");
            Check(ai.currentTween==null&&ai.attackSequence==null,"cleared");
            ai.currentTween=new Tween();ai.attackSequence=new Sequence();
            ai.SetStunned(true);Check(!ai.currentTween.playing&&!ai.attackSequence.playing,"fresh pause");
            ai.SetStunned(false);Check(ai.currentTween.playing&&ai.attackSequence.playing,"fresh resume");
        });
        Test("OnDeath後の復帰通知で攻撃を復活させない",()=>{
            var ai=new ExtractedBossSpeed();var movement=ai.currentTween;var attack=ai.attackSequence;
            ai.SetStunned(true);ai.OnDeath();ai.SetStunned(false);
            Check(!movement.active&&!attack.active,"dead tweens");
            Check(movement.playCalls==0&&attack.playCalls==0,"no resurrection");
        });
        Test("オーバーヒート等で設定済みのtimeScaleを保持",()=>{
            var ai=new ExtractedBossSpeed();ai.currentTween.timeScale=0.3f;ai.attackSequence.timeScale=1f;
            ai.SetStunned(true);ai.SetStunned(false);
            Check(ai.currentTween.timeScale==0.3f&&ai.attackSequence.timeScale==1f,"time scales");
        });
        Console.WriteLine("RESULT passed="+passed+" failed="+failed);
        return failed==0?0:1;
    }
}
'''

with tempfile.TemporaryDirectory(prefix="gearcraft-ar-stun-callback-", dir="/private/tmp") as work:
    harness = Path(work) / "Harness.cs"
    executable = Path(work) / "Harness.exe"
    harness.write_text(code)
    compiled = subprocess.run(
        [MONO, MCS, "-out:" + str(executable), str(harness)],
        capture_output=True, text=True,
    )
    if compiled.returncode == 0:
        ran = subprocess.run([MONO, str(executable)], capture_output=True, text=True)
    else:
        ran = None

unchanged = SOURCE.read_text() == source_text
report = "\n".join([
    "AR-227 スタン停止・復帰コールバック回帰テスト",
    "実施日時: " + datetime.now().astimezone().isoformat(timespec="seconds"),
    "実装 SHA-256: " + hashlib.sha256(source_text.encode()).hexdigest(),
    "抽出メソッド SHA-256: " + hashlib.sha256(methods.encode()).hexdigest(),
    "検証中の実装変更なし: " + str(unchanged),
    "SetStunned / StopTweens / OnDisable / OnDeath を実ソースから機械的に抽出して実行。",
    "Tween は呼出し記録用のテストダブル。Unity/DOTween 実ランタイムでの位置・時間・発射の確認ではない。",
    "生成した一時ソース・実行ファイルは /private/tmp から削除済み。ゲームアプリのビルドなし。",
    "コンパイル終了コード: " + str(compiled.returncode),
    compiled.stdout + compiled.stderr or "コンパイル出力なし（エラー・警告なし）",
    "実行終了コード: " + str(ran.returncode if ran else "未実行"),
    (ran.stdout + ran.stderr) if ran else "",
])
REPORT.write_text(report)
print(report)
raise SystemExit(0 if compiled.returncode == 0 and ran and ran.returncode == 0 and unchanged else 1)
