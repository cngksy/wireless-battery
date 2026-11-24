// GPL-3.0-or-later. Transport and battery layout verified by erzhenin-alex:
// https://github.com/erzhenin-alex/Wireless-Device-Battery-Monitor/commit/6ff09641e99b68b647f034f2ed91cb6aac75c292
using System;
using System.Diagnostics;

internal static class LogitechG735 {
    internal const ushort Vendor = 0x046D;
    internal const ushort Product = 0x0AD8;
    internal const string DeviceKey = "headset:logitech-g735";
    private const int MessageOffset = 16;
    private const int TotalTimeoutMs = 1200;
    // The low nibble identifies our calls, independently of G HUB's software ID.
    private const byte ReadFunction = 0x0B;
    private static readonly byte[] RequestHeader = {
        0xF2, 0x01, 0x05, 0x1E, 0xFA, 0x82, 0x40, 0xCE,
        0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x00
    };
    private static readonly byte[] ReplyPrefix = {
        0xF2, 0x00, 0x00, 0x0C, 0x1E, 0x82, 0x40, 0x00, 0x00, 0xF2, 0x00
    };

    internal static bool Matches(HidInfo info) {
        return info != null && info.Vid == Vendor && info.Pid == Product
            && info.Page == 0xFF03 && info.Usage == 1
            && info.Input == 271 && info.Output == 271;
    }

    internal static bool TryRead(HidInfo info, out int percent, out byte chargeState) {
        percent = -1;
        chargeState = 0;
        if (!Matches(info)) return false;

        using (var channel = new HidChannel(info)) {
            if (!channel.Valid) return false;
            var clock = Stopwatch.StartNew();
            // Root getFeature(0x0104) returns the current index of CenturionBatterySoc.
            byte[] feature = ReadFunctionZero(channel, clock, 0, 0x01, 0x04);
            if (feature == null || feature[0] == 0 || feature[0] == 0xFF) return false;
            byte[] battery = ReadFunctionZero(channel, clock, feature[0]);
            return TryDecodeBattery(battery, out percent, out chargeState);
        }
    }

    private static byte[] ReadFunctionZero(HidChannel channel, Stopwatch clock, byte index, params byte[] args) {
        var frame = new byte[MessageOffset + 2 + args.Length];
        Array.Copy(RequestHeader, frame, MessageOffset);
        frame[MessageOffset] = index;
        frame[MessageOffset + 1] = ReadFunction;
        Array.Copy(args, 0, frame, MessageOffset + 2, args.Length);
        int remaining = TotalTimeoutMs - (int)clock.ElapsedMilliseconds;
        if (remaining <= 0 || !channel.Send(frame, Math.Min(250, remaining))) return null;

        while ((remaining = TotalTimeoutMs - (int)clock.ElapsedMilliseconds) > 0) {
            byte[] report = channel.Read(remaining);
            if (report == null) return null;
            if (report.Length >= MessageOffset + 4 && report[MessageOffset] == 0xFF
                && report[MessageOffset + 1] == index && report[MessageOffset + 2] == ReadFunction
                && HasReplyPrefix(report)) return null;
            byte[] payload = ReplyData(report, index);
            if (payload != null) return payload;
        }
        return null;
    }

    internal static byte[] ReplyData(byte[] report, byte index) {
        int payload = MessageOffset + 2;
        if (report == null || report.Length < payload + 3 || !HasReplyPrefix(report)
            || report[MessageOffset] != index || report[MessageOffset + 1] != ReadFunction) return null;
        return new[] { report[payload], report[payload + 1], report[payload + 2] };
    }

    internal static bool TryDecodeBattery(byte[] data, out int percent, out byte chargeState) {
        percent = -1;
        chargeState = 0;
        if (data == null || data.Length < 3 || data[0] > 100) return false;
        percent = data[0];
        chargeState = data[2];
        return true;
    }

    private static bool HasReplyPrefix(byte[] report) {
        for (int i = 0; i < ReplyPrefix.Length; i++)
            if (report[i] != ReplyPrefix[i]) return false;
        return true;
    }
}
