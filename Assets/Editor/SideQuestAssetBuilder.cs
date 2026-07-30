using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// One-shot editor tool that creates the 9 side-quest QuestData assets and 7
/// new JournalData assets defined in Docs/SideQuests_Proposal.json.
/// Run via menu: Tools/Side Quests/Build Side Quest Assets.
/// Idempotent: re-running overwrites existing assets with the proposal content.
/// </summary>
public static class SideQuestAssetBuilder
{
    private const string QuestsDir = "Assets/Resources/SideQuests";
    private const string JournalsDir = "Assets/Resources/Journals";

    [MenuItem("Tools/Side Quests/Build Side Quest Assets")]
    public static void Build()
    {
        Directory.CreateDirectory(QuestsDir);
        Directory.CreateDirectory(JournalsDir);

        // ---- 7 new JournalData assets ----
        CreateJournal("NeighborJournal_19", 36, JournalCategory.NeighborJournal,
            "Nhật ký hàng xóm #19 — Linh mục cuối cùng",
            "Tôi là cha Pedro, linh mục của nhà thờ nhỏ giữa thành phố. Khi dịch bệnh bắt đầu, người dân kéo đến đây trú ẩn — tôi nhớ có gia đình Zoe, ông Mathew, và vài người nữa. Chúng tôi cầu nguyện mỗi đêm. Nhưng rồi Zoe bắt đầu ho sốt, rồi Mathew cũng vậy. Tôi hiểu rồi — nhà thờ không thể bảo vệ ai. Tôi phải bỏ họ lại, bỏ Chúa lại, để tự cứu mình. Xin tha thứ cho tôi.");

        CreateJournal("MilitaryRecord_07", 37, JournalCategory.MilitaryRecord,
            "Báo cáo quân đội #7 — Kế hoạch thoát",
            "Đại úy Reyes đây. Chúng tôi tìm thấy một chiếc xe tải quân sự còn nguyên vẹn ở trạm sửa xe bên cạnh khu dân cư. Kíp của tôi — 4 người — đã cố sửa nó trong hai ngày. Nhưng zombie nghe tiếng máy và kéo đến hàng chục. Chỉ còn tôi và Taylor. Taylor bị cắn hôm qua. Tôi một mình không thể sửa nổi. Nếu ai đọc được này: chiếc xe vẫn ở đó, chỉ thiếu bộ đề. Tìm bộ đề ở kho phụ tùng.");

        CreateJournal("NeighborJournal_20", 38, JournalCategory.NeighborJournal,
            "Nhật ký hàng xóm #20 — Đêm cuối cùng",
            "Tôi làm việc ở Motel này 6 năm. Đêm cuối cùng, phòng 12 gọi lên — khách bảo thấy hàng xóm phòng 11 'không ổn'. Tôi lên kiểm tra, anh ta đang gầm và lao vào tôi. Tôi chạy, khóa cửa văn phòng. Suốt đêm nghe tiếng đập cửa. Buổi sáng, im ắng. Tôi mở cửa — hành lang đầy máu, không ai còn. Tôi lấy chìa khóa xe khách, bỏ đi. Chúc ai đọc được may mắn.");

        CreateJournal("CureRecord_04", 39, JournalCategory.CureRecord,
            "Ghi chép thuốc giải #4 — Ca nhiễm đầu tiên",
            "Hồ sơ y tế — Khu quarantine quân đội. Bệnh nhân #001 (giấu tên): nam, 28 tuổi, nhập khu cách ly sau tiếp xúc với bệnh nhân zero tại bệnh viện. Triệu chứng: sốt cao 39°C, ho khan, sau 6 giờ bắt đầu co giật, sau 12 giờ mất nhận thức, sau 18 giờ biến đổi hoàn toàn. Thời gian ủ bệnh ước tính 2-4 giờ từ lúc tiếp xúc. KHÔNG có thuốc. Khuyến nghị: cách ly tuyệt đối 24 giờ, nếu sau 24 giờ không có triệu chứng thì an toàn.");

        CreateJournal("MilitaryRecord_08", 40, JournalCategory.MilitaryRecord,
            "Báo cáo quân đội #8 — Báo cáo thương vong",
            "Đại tá Morrison — báo cáo thương vong cuối cùng. Tính đến 04:00 ngày 14/10: Thiếu tá Chen — KIA. Trung úy Park — KIA. Trung sĩ Lopez — MIA (bị cắn, tự cách ly). Hơn 200 lính thương vong. Chúng tôi mất kiểm soát hoàn toàn khu dân cư và chung cư. Lệnh cuối: toàn quân rút về căn cứ phía nam, để lại vũ khí và hồ sơ. Nếu ai còn sống đọc được: lấy bộ kích nổ ở tòa cao tầng gần cầu, kích hoạt quả bom ở trạm phát sóng. Đây là lệnh cuối cùng của tôi.");

        CreateJournal("NeighborJournal_21", 41, JournalCategory.NeighborJournal,
            "Nhật ký hàng xóm #21 — Bức thư cuối",
            "Tôi viết thay cho nhiều người mẹ trong chung cư này. Bà Hoa tầng 8 — gửi con gái ở Mỹ: 'Con ơi, mẹ yêu con, đừng về, thành phố không còn an toàn.' Bà Lan tầng 12 — gửi con trai ở Đà Nẵng: 'Mẹ không tiếc gì, chỉ tiếc không được ôm con lần cuối.' Bà Nguyệt tầng 15 — gửi chồng: 'Anh hãy sống vì con.' Chúng tôi viết vì hy vọng ai đọc được sẽ mang tin về cho con cái chúng tôi. Xin hãy nói với chúng rằng mẹ chúng đã nghĩ về chúng đến giây cuối cùng.");

        CreateJournal("BrotherJournal_05", 42, JournalCategory.BrotherJournal,
            "Nhật ký anh trai #5 — Lá thư gửi em",
            "Em yêu của anh, nếu em đọc được dòng này — anh xin lỗi. Anh đã tình nguyện thử thuốc của bác sĩ Edward vì anh nghĩ mình có thể cứu được mọi người. Nhưng anh đã sai. Anh cảm nhận mình đang mất kiểm soát — tay anh run, đầu anh đau, và có tiếng gì đó trong đầu anh bảo anh 'đi tìm ăn'. Anh sẽ không còn là anh nữa. Em ơi, anh yêu em. Hãy tìm thuốc giải — bác sĩ Edward nói nó ở đâu đó trong thành phố. Đừng buồn, đừng hận anh. Anh tự chọn con đường này. Hãy sống vì cả hai đứa.");

        // ---- 9 QuestData assets ----
        // Quest 101 — Ch1 — Kho vũ khí bị bỏ quên
        CreateQuest("SideQuest_101_ForgottenArmory", 101, 1,
            "Kho vũ khí bị bỏ quên",
            "Bên cạnh bệnh viện là hai nhà kho bỏ hoang. Tiếng động bên trong gợi ý có thứ gì đó đáng giá — nhưng cũng không an toàn.",
            "Khám phá 2 nhà kho, tiêu diệt zombie bên trong, loot vật tư",
            150, "SoldierJournal_02");

        // Quest 102 — Ch2 — Ngọn hải đăng cuối cùng
        CreateQuest("SideQuest_102_HeliPad", 102, 2,
            "Ngọn hải đăng cuối cùng",
            "Trên tầng thượng bệnh viện có một helipad. Đội trực thăng cứu thương cuối cùng đã để lại gì đó...",
            "Leo lên tầng thượng bệnh viện, đến helipad, tìm bản ghi cuối của đội y tế",
            100, "DoctorJournal_02");

        // Quest 103 — Ch4 — Nhà thờ bị phong toả
        CreateQuest("SideQuest_103_Church", 103, 4,
            "Nhà thờ bị phong toả",
            "Một nhà thờ cũ giữa đường từ công trường sang khu dân cư. Cửa còn nguyên, bên trong có thể còn người...",
            "Vào nhà thờ, thu thập 2 nhật ký của những người trú ẩn cuối cùng",
            200, "NeighborJournal_19");

        // Quest 104 — Ch3 — Trạm xăng chết
        CreateQuest("SideQuest_104_AutoRepair", 104, 3,
            "Trạm xăng chết",
            "Trạm sửa xe bên cạnh khu dân cư. Chủ nó — Jack — từng nói về việc sửa một chiếc xe tải để thoát khỏi thành phố. Có lẽ cuốn nhật ký của ông ta vẫn còn...",
            "Khám phá trạm sửa xe, tiêu diệt zombie, tìm nhật ký của thợ xe Jack",
            200, "MilitaryRecord_07");

        // Quest 105 — Ch4 — Đêm cuối cùng ở Motel
        CreateQuest("SideQuest_105_Motel", 105, 4,
            "Đêm cuối cùng ở Motel",
            "Dãy Motel bỏ hoang. Những người khách cuối cùng đã để lại nhật ký trước khi mọi thứ sụp đổ...",
            "Khám phá 2 dãy Motel, thu thập 3 nhật ký của khách trọ cuối cùng",
            250, "NeighborJournal_20");

        // Quest 106 — Ch4 — Lều quarantine
        CreateQuest("SideQuest_106_Quarantine", 106, 4,
            "Lều quarantine — Nơi bệnh bắt đầu",
            "Khu lều quarantine quân đội — nơi đầu tiên phát hiện dịch bệnh. Hồ sơ y tế có thể còn trong lều chỉ huy...",
            "Khám phá khu lều quarantine, sống sót qua 3 wave zombie, tìm hồ sơ y tế đầu tiên",
            350, "CureRecord_04");

        // Quest 107 — Ch5 — Căn cứ quân đội cuối cùng
        CreateQuest("SideQuest_107_HighRiseBase", 107, 5,
            "Căn cứ quân đội cuối cùng",
            "Toà nhà cao tầng bên cạnh cầu — quân đội từng dùng làm căn cứ tạm. Lệnh rút lui và nhật ký kíp công binh có thể vẫn còn...",
            "Vào toà nhà cao tầng, tiêu diệt zombie, thu thập 2 hồ sơ quân đội cuối cùng",
            300, "MilitaryRecord_08");

        // Quest 108 — Ch5 — Câu chuyện của những người mẹ
        CreateQuest("SideQuest_108_MothersStory", 108, 5,
            "Câu chuyện của những người mẹ",
            "Ba toà nhà chung cư vẫn còn những cuốn nhật ký của người ở lại. Câu chuyện của họ — những người mẹ, người cha, người già — cần được ghi nhớ...",
            "Khám phá 3 toà nhà chung cư, thu thập 4 nhật ký của cư dân cuối cùng",
            300, "NeighborJournal_21");

        // Quest 109 — Ch5 — Ngọn hải đăng bí ẩn
        CreateQuest("SideQuest_109_Lighthouse", 109, 5,
            "Ngọn hải đăng bí ẩn",
            "Ngoài rìa thành phố, ngọn hải đăng vẫn đứng vững. Ai đó đã để lại ghi chép quan trọng — về bệnh nhân 001, về người anh trai...",
            "Đi đến ngọn hải đăng cuối cùng của thành phố, tìm ghi chép thuốc giải cuối cùng",
            400, "BrotherJournal_05");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SideQuestAssetBuilder] Created 9 side-quest QuestData + 7 JournalData assets.");
    }

    private static void CreateJournal(string fileName, int id, JournalCategory category, string title, string content)
    {
        var path = $"{JournalsDir}/{fileName}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<JournalData>(path);
        bool created = false;
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<JournalData>();
            created = true;
        }
        asset.id = id;
        asset.category = category;
        asset.title = title;
        asset.content = content;
        if (created)
            AssetDatabase.CreateAsset(asset, path);
        else
            EditorUtility.SetDirty(asset);
        Debug.Log($"[SideQuestAssetBuilder] {(created ? "Created" : "Updated")} journal: {path}");
    }

    private static void CreateQuest(string fileName, int questId, int chapter, string title, string description, string objective, float expReward, string journalRewardName)
    {
        var path = $"{QuestsDir}/{fileName}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<QuestData>(path);
        bool created = false;
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<QuestData>();
            created = true;
        }
        asset.questId = questId;
        asset.chapter = chapter;
        asset.title = title;
        asset.description = description;
        asset.objective = objective;
        asset.expReward = expReward;

        // Wire journal reward by name (load from Resources/Journals).
        if (!string.IsNullOrEmpty(journalRewardName))
        {
            var journalPath = $"{JournalsDir}/{journalRewardName}.asset";
            asset.journalReward = AssetDatabase.LoadAssetAtPath<JournalData>(journalPath);
            if (asset.journalReward == null)
                Debug.LogWarning($"[SideQuestAssetBuilder] Journal reward not found: {journalPath}");
        }

        if (created)
            AssetDatabase.CreateAsset(asset, path);
        else
            EditorUtility.SetDirty(asset);
        Debug.Log($"[SideQuestAssetBuilder] {(created ? "Created" : "Updated")} quest: {path}");
    }
}
