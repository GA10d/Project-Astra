namespace AstraCabin.Galaxies.Zyl
{
    static class MihuoerActing
    {
        // Stable authored line keys keep the direction attached to each line when dialogue is reordered.
        public static MihuoerPose Standing(MihuoerDialoguePlayer dialogue, MihuoerPose talking, MihuoerPose quiet)
        {
            if (dialogue?.Current == null) return quiet;
            switch (dialogue.Current.timerKey)
            {
                // Trapped inside: numbness, panic, point out the timer, then look for a way out.
                case "timer_mw61emp8_zxsxiem5xi": return MihuoerPose.Slumped;
                case "timer_mw61emp8_npllgzed42": return MihuoerPose.OpenHands;
                case "timer_mw61emp8_7id2bgwjwd": return MihuoerPose.HeadClutch;
                case "timer_mw61emp8_uh2xm70wzu": return MihuoerPose.Idle;
                case "timer_mw61emp8_7pcczfersi": return MihuoerPose.Slumped;
                case "timer_mw61emp8_eektw4y9ro": return MihuoerPose.TempleTouch;
                case "timer_mw61emp8_3ncz4cf0em": return MihuoerPose.ChinThink;

                // The pole fails and the amount of debris finally sinks in.
                case "timer_mw6d1jo0_b36y85n23x": return MihuoerPose.Slumped;
                case "timer_mw5zq1ws_kn5ku26shs": return MihuoerPose.Emphasis;
                case "timer_mw6d1jo0_9octlxlb8m": return MihuoerPose.OpenHands;
                case "timer_mw5zq1ws_gyahmnqdde": return MihuoerPose.KneeBrace;
                case "timer_mw6d1jo0_mnyt6e0c2v": return MihuoerPose.TempleTouch;
                case "timer_mw6d1jo0_d9ys6p0cqh": return MihuoerPose.OpenHands;

                // The rant gives way to regret, recollection and renewed resolve.
                case "timer_mw5zq1ws_eh7crjxcx9": return MihuoerPose.OpenHands;
                case "timer_mw6d1jo0_y1ths0ll03": return MihuoerPose.Emphasis;
                case "timer_mw6d1jo0_1f6p80czg6": return MihuoerPose.Explain;
                case "timer_mw6d1jo0_ak8w3vrweq": return MihuoerPose.AngryFists;
                case "timer_mw6d1jo0_9jul5x0bu7": return MihuoerPose.ChinThink;
                case "timer_mw6d1jo0_jvzb4qqxu5": return MihuoerPose.Slumped;
                case "timer_mw6d1jo0_j95sl7je4v": return MihuoerPose.TempleTouch;
                case "timer_mw6d1jo0_anu3zq7xbi": return MihuoerPose.Explain;
                case "timer_mw6d1jo0_etihiue5lq": return MihuoerPose.OpenHands;
                case "timer_mw6d1jo0_q2pp68bgc9": return MihuoerPose.Slumped;
                case "timer_mw6d1jo0_nkrlxubedy": return MihuoerPose.ChinThink;
                case "timer_mw6d1jo0_ln80ylvt9p": return MihuoerPose.Explain;
                case "timer_mw6d1jo0_kazjd5ktuv": return MihuoerPose.OpenHands;
                case "timer_mw6d1jo0_7jsp367xjh": return MihuoerPose.Slumped;
                case "timer_mw6d1jo0_tnx1mkizh6": return MihuoerPose.Explain;
                case "timer_mw6d1jo0_l4odl7u8vd": return MihuoerPose.AngryFists;
                case "timer_mw6d1jo0_ud530kf3j4": return MihuoerPose.Emphasis;

                case "timer_mw5zq1ws_6e8z4sfd8p": return MihuoerPose.Explain;
                case "timer_mw6d1jo0_rivtznj5am": return MihuoerPose.Slumped;
                case "timer_mw6d1jo0_lki56hbr4m": return MihuoerPose.Idle;
                case "timer_mw6d1jo0_w5grby62w3": return MihuoerPose.ChinThink;
                case "timer_mw6d1jo0_zo0oi7dcm8": return MihuoerPose.Explain;
                case "timer_mw6d1jo0_xznhfqrgsf": return MihuoerPose.ChinThink;
                case "timer_mw6d1jo0_uydph4gsng": return MihuoerPose.Idle;
                case "timer_mw6d1jo0_vfossn0fwb": return MihuoerPose.OpenHands;
                case "timer_mw6d1jo0_r0t6nr976g": return MihuoerPose.Emphasis;
                case "timer_mw6d1jo0_0c4g94xdqj": return MihuoerPose.Explain;

                case "timer_mw5zq1ws_86iltm7gvt": return MihuoerPose.Slumped;
                case "timer_mw61emp8_71mkfgo3dp": return MihuoerPose.ChinThink;
                case "timer_mw61emp8_2b1jbbr5wo": return MihuoerPose.Explain;
                case "timer_mw61emp8_9gkhlp0li1": return MihuoerPose.OpenHands;
                case "timer_mw61emp8_14x2btsj0g": return MihuoerPose.Explain;
                case "timer_mw61emp8_q7ijumpe3t":
                    return dialogue.Elapsed < dialogue.Current.duration * .52f ? MihuoerPose.Explain : MihuoerPose.StandWave;
                default: return talking;
            }
        }

        public static MihuoerPose Seated(MihuoerDialoguePlayer dialogue)
        {
            if (dialogue?.Current == null) return MihuoerPose.ChairListen;
            switch (dialogue.Current.timerKey)
            {
                case "timer_mw61emp8_qmmmckiuej":
                case "timer_mw61emp8_ilx7eq455a":
                case "timer_mw61emp8_oa19j89pgj":
                case "timer_mw623ddz_l37clzgacc":
                case "timer_mw4rv83v_1atazmsmuq":
                    return MihuoerPose.ChairWorry;
                case "timer_mw61emp8_15wojpd89u":
                case "timer_mw4rlejw_pgx1ith7k9":
                case "timer_mw623ddz_0s8q7u6rbh":
                case "timer_mw4rs1ln_1y4yq56mf8":
                case "timer_mw623ddz_qyggik2vuy":
                    return MihuoerPose.ChairEmphasis;
                default:
                    return MihuoerPose.ChairTalk;
            }
        }
    }
}
