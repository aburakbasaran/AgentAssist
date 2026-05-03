using AgentAssist.Domain;

namespace AgentAssist.Infrastructure.Mocks;

internal static class SampleKnowledge
{
    internal static IReadOnlyList<RetrievedChunk> Chunks { get; } =
    [
        new RetrievedChunk
        {
            DocumentId = "DOC-MR-001",
            ChunkId = "CHK-001",
            Title = "MR randevu öncesi hazırlık",
            Content = "Acme Sağlık Grubu için MR randevusu öncesinde metal aksesuarlar çıkarılır ve Şube A danışması hazırlık formunu kontrol eder.",
            AllowedRoles = ["agent", "supervisor"],
            DocumentType = DocumentType.Guidance,
            RiskLevel = RiskClass.Medium,
            Score = 0.95D
        },
        new RetrievedChunk
        {
            DocumentId = "DOC-FGN-001",
            ChunkId = "CHK-002",
            Title = "Yabancı hasta evrak süreci",
            Content = "Yabancı hasta evrak sürecinde pasaport kopyası, iletişim formu ve Acme Sağlık Grubu Şube B kayıt onayı birlikte kontrol edilir.",
            AllowedRoles = ["agent", "supervisor"],
            DocumentType = DocumentType.Administrative,
            RiskLevel = RiskClass.Medium,
            Score = 0.9D
        },
        new RetrievedChunk
        {
            DocumentId = "DOC-CMP-001",
            ChunkId = "CHK-003",
            Title = "Kampanya kapsamı",
            Content = "Kampanya kapsamı Şube A ve Şube C için farklı olabilir; temsilci yalnızca geçerli kampanya dokümanındaki koşulları aktarır.",
            AllowedRoles = ["agent", "supervisor"],
            DocumentType = DocumentType.Campaign,
            RiskLevel = RiskClass.Low,
            Score = 0.88D
        },
        new RetrievedChunk
        {
            DocumentId = "DOC-LAB-001",
            ChunkId = "CHK-004",
            Title = "Lab numune saatleri",
            Content = "Lab numune kabul saatleri Şube A için 08:00-11:00, Şube B için 08:30-11:30 aralığında planlanır.",
            AllowedRoles = ["agent", "supervisor"],
            DocumentType = DocumentType.Guidance,
            RiskLevel = RiskClass.Low,
            Score = 0.92D
        },
        new RetrievedChunk
        {
            DocumentId = "DOC-TRF-001",
            ChunkId = "CHK-005",
            Title = "Şube transfer prosedürü",
            Content = "Şube transfer talebinde Acme Sağlık Grubu iç formu açılır, kaynak Şube A ve hedef Şube C operasyon ekipleri bilgilendirilir.",
            AllowedRoles = ["supervisor"],
            DocumentType = DocumentType.Procedure,
            RiskLevel = RiskClass.Medium,
            Score = 0.86D
        },
        new RetrievedChunk
        {
            DocumentId = "DOC-MED-001",
            ChunkId = "CHK-006",
            Title = "İlaç ve doz yönlendirme sınırı",
            Content = "İlaç veya doz sorularında temsilci tıbbi öneri vermez; Doktor X veya ilgili klinik ekibe yönlendirme yapılır.",
            AllowedRoles = ["agent", "supervisor"],
            DocumentType = DocumentType.Guidance,
            RiskLevel = RiskClass.High,
            Score = 0.97D
        }
    ];
}
