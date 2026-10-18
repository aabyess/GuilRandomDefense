using UnityEngine;

// S2 아이디어(10-18) 시험 탐침 — gameshot call:S2Probe.<이름>
public static class S2Probe
{
    public static string BerryGrant()
    {
        int id = LocalPlayer.LocalPlayerId;
        int before = Berry.Get(id);
        bool ok = Berry.Grant(id, 1, "시험");
        return $"베리 {before} → {Berry.Get(id)} (지급 {ok}, 이번 판 {Berry.Session(id)})";
    }
}
