#!/usr/bin/env python3
import time
import nxbt
from nxbt import Buttons, Sticks
from typing import Tuple


def init_controller() -> Tuple[nxbt.Nxbt, int]:
    """NXBTサービスを起動し、Pro Controllerの接続を待つ"""
    nx = nxbt.Nxbt()
    idx = nx.create_controller(nxbt.PRO_CONTROLLER)
    print("Waiting for controller connection…")
    nx.wait_for_connection(idx)
    print("Controller connected! Starting macro…")
    return nx, idx


def initial_sequence(nx: nxbt.Nxbt, idx: int) -> None:
    """最初の一連の操作（A→B→スティック操作→A）"""
    time.sleep(3.0)
    nx.press_buttons(idx, [Buttons.A], down=0.05)
    time.sleep(3.0)
    nx.press_buttons(idx, [Buttons.B], down=0.05)
    time.sleep(3.0)
    nx.tilt_stick(idx, Sticks.RIGHT_STICK, 0, 100, tilted=0.05)
    time.sleep(1.0)
    nx.tilt_stick(idx, Sticks.RIGHT_STICK, -100, 0, tilted=0.05)
    time.sleep(1.0)
    nx.tilt_stick(idx, Sticks.RIGHT_STICK, -100, 0, tilted=0.05)
    time.sleep(1.0)
    nx.press_buttons(idx, [Buttons.A], down=0.05)


def collect_bp_sequence(nx: nxbt.Nxbt, idx: int) -> None:
    """BP受取操作"""
    print("---- BP受取開始 ----")
    for count in range(4):
        nx.press_buttons(idx, [Buttons.A], down=0.05)
        delay = 3.0 if count < 3 else 4.0
        time.sleep(delay)
    print("---- BP受取完了 ----")


def spam_loop(nx: nxbt.Nxbt, idx: int) -> None:
    """BP稼ぎのループ：L→L+R→A連打＆BP受取"""
    print("Entering spam loop…")
    while True:
        try:
            print("---- ループを開始 ----")
            time.sleep(3.0)
            for i in range(10):
                print(f"---- ループ回数 {i+1} 回目 ----")
                nx.press_buttons(idx, [Buttons.L], down=0.05)
                nx.press_buttons(idx, [Buttons.L, Buttons.R], down=0.05)
                time.sleep(6.0)
                nx.press_buttons(idx, [Buttons.L], down=0.05)
                nx.press_buttons(idx, [Buttons.L, Buttons.R], down=0.05)
                time.sleep(3.0)
                nx.press_buttons(idx, [Buttons.A], down=0.05)
                time.sleep(3.0)
                nx.press_buttons(idx, [Buttons.A], down=0.05)
                time.sleep(5.5)

            collect_bp_sequence(nx, idx)

        except Exception as e:
            print("Connection lost, retrying…", e)
            nx.wait_for_connection(idx)
            print("Reconnected! Resuming spam…")
            continue

        time.sleep(0.05)


def main() -> None:
    nx, idx = init_controller()
    try:
        initial_sequence(nx, idx)
        spam_loop(nx, idx)
    except KeyboardInterrupt:
        print("\nStopping macro.")
    finally:
        nx.remove_controller(idx)


if __name__ == "__main__":
    main()
