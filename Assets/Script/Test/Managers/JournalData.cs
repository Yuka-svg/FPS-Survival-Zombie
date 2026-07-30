using UnityEngine;

/// <summary>
/// Category a journal/collectible belongs to. Used to group entries in the
/// journal gallery and to evaluate ending conditions (e.g. True Ending
/// requires collecting every journal across all categories).
/// </summary>
public enum JournalCategory
{
    DoctorJournal,      // Nhật ký bác sĩ
    SoldierJournal,     // Nhật ký người lính vô danh
    BrotherJournal,     // Nhật ký anh trai
    NeighborJournal,    // Nhật ký hàng xóm
    ExperimentReport,   // Báo cáo thí nghiệm
    MilitaryRecord,     // Ghi chép quân đội
    CureRecord          // Ghi chép thuốc giải
}

[CreateAssetMenu(menuName = "Collectibles/Journal")]
public class JournalData : ScriptableObject
{
    public int id;

    public string title;

    [Tooltip("Category used for grouping in the journal UI and ending evaluation.")]
    public JournalCategory category;

    [TextArea(10, 20)]
    public string content;

    public Sprite image;

    public AudioClip voiceLog;
}
