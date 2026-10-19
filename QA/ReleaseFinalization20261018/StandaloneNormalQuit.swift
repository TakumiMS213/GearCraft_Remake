// QA helper: request normal termination of the one PID held by the smoke runner.
// No input events, screenshots, permission prompts, signals, or forceTerminate.
import Foundation
import AppKit

func emit(_ data: [String: Any]) {
    if let bytes = try? JSONSerialization.data(withJSONObject: data, options: [.sortedKeys]),
       let line = String(data: bytes, encoding: .utf8) {
        print(line)
        fflush(stdout)
    }
}

guard CommandLine.arguments.count == 3,
      let pid = Int32(CommandLine.arguments[1]), pid > 0 else {
    emit(["accepted": false, "reason": "Expected owned PID and absolute executable path"])
    exit(2)
}
let expected = URL(fileURLWithPath: CommandLine.arguments[2]).resolvingSymlinksInPath().path
guard let app = NSRunningApplication(processIdentifier: pid) else {
    emit(["accepted": false, "pid": pid, "reason": "Application lookup failed; no termination attempted"])
    exit(3)
}
guard let executable = app.executableURL?.resolvingSymlinksInPath().path,
      executable == expected else {
    emit(["accepted": false, "pid": pid, "reason": "Executable identity mismatch; no termination attempted"])
    exit(4)
}
let accepted = app.terminate()
emit(["accepted": accepted, "pid": pid, "alreadyTerminated": app.isTerminated,
      "reason": accepted ? "Normal termination request sent; caller must wait for owned process exit" : "Normal termination request rejected; no fallback attempted"])
exit(accepted ? 0 : 5)
