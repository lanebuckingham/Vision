namespace Vision.CredentialService.Infrastructure.Persistence.Seeding;

/// <summary>
/// Deterministic GUIDs for seed data. Stable IDs ensure idempotent seeding.
/// </summary>
public static class SeedDataIds
{
    // People
    public static readonly Guid PersonJamesWilson = new("d1a2b3c4-0001-4ccc-a001-100000000001");
    public static readonly Guid PersonMariaGarcia = new("d1a2b3c4-0002-4ccc-a002-100000000002");
    public static readonly Guid PersonRobertKim = new("d1a2b3c4-0003-4ccc-a003-100000000003");
    public static readonly Guid PersonEmilyCarter = new("d1a2b3c4-0004-4ccc-a004-100000000004");
    public static readonly Guid PersonMichaelBrown = new("d1a2b3c4-0005-4ccc-a005-100000000005");
    public static readonly Guid PersonJessicaDavis = new("d1a2b3c4-0006-4ccc-a006-100000000006");
    public static readonly Guid PersonAndrewNguyen = new("d1a2b3c4-0007-4ccc-a007-100000000007");
    public static readonly Guid PersonRachelThompson = new("d1a2b3c4-0008-4ccc-a008-100000000008");
    public static readonly Guid PersonDanielMartinez = new("d1a2b3c4-0009-4ccc-a009-100000000009");
    public static readonly Guid PersonOliviaWright = new("d1a2b3c4-0010-4ccc-a010-10000000000a");
    public static readonly Guid PersonChrisLee = new("d1a2b3c4-0011-4ccc-a011-10000000000b");
    public static readonly Guid PersonAmandaHall = new("d1a2b3c4-0012-4ccc-a012-10000000000c");
    public static readonly Guid PersonKevinScott = new("d1a2b3c4-0013-4ccc-a013-10000000000d");
    public static readonly Guid PersonNatalieRoss = new("d1a2b3c4-0014-4ccc-a014-10000000000e");
    public static readonly Guid PersonBrianTaylor = new("d1a2b3c4-0015-4ccc-a015-10000000000f");
    public static readonly Guid PersonSophiaAdams = new("d1a2b3c4-0016-4ccc-a016-100000000010");
    public static readonly Guid PersonJasonClark = new("d1a2b3c4-0017-4ccc-a017-100000000011");
    public static readonly Guid PersonMeganWhite = new("d1a2b3c4-0018-4ccc-a018-100000000012");

    // Demo credential — the "lost badge" person
    public static readonly Guid PersonLostBadge = PersonMichaelBrown;
    public static readonly Guid CredentialLostBadge = new("e1a2b3c4-0005-4ddd-a005-500000000005");
}
