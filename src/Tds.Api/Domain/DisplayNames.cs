namespace Tds.Api.Domain;

/// <summary>
/// Bosnian display names for enum values inside server error messages ("Lot L-1 nije prihvaćen (Karantin)").
/// The UI source of truth is web/src/lib/labels.ts; DisplayNamesMatchLabelsTsTests fails if they drift apart.
/// </summary>
public static class DisplayNames
{
    public static string Of(WorkOrderStatus s) => s switch
    {
        WorkOrderStatus.Zaprimljen => "Zaprimljen",
        WorkOrderStatus.CekaMaterijal => "Čeka materijal",
        WorkOrderStatus.Spreman => "Spreman",
        WorkOrderStatus.Laser => "Laser",
        WorkOrderStatus.Presanje => "Presanje",
        WorkOrderStatus.Kontrola => "Kontrola",
        WorkOrderStatus.Doraditi => "Doraditi",
        WorkOrderStatus.Zavrseno => "Završeno",
        WorkOrderStatus.Isporuceno => "Isporučeno",
        WorkOrderStatus.Otkazano => "Otkazano",
        _ => s.ToString(),
    };

    public static string Of(ReceiptDecision d) => d switch
    {
        ReceiptDecision.NaKontroli => "Na kontroli",
        ReceiptDecision.Prihvacen => "Prihvaćen",
        ReceiptDecision.Karantin => "Karantin",
        ReceiptDecision.Odbijen => "Odbijen",
        _ => d.ToString(),
    };

    public static string Of(SupplierApprovalStatus s) => s switch
    {
        SupplierApprovalStatus.UProcjeni => "U procjeni",
        SupplierApprovalStatus.Odobren => "Odobren",
        SupplierApprovalStatus.UslovnoOdobren => "Uslovno odobren",
        SupplierApprovalStatus.Blokiran => "Blokiran",
        _ => s.ToString(),
    };

    public static string Of(InspectionPhase p) => p switch
    {
        InspectionPhase.UlazniMaterijal => "Ulazni materijal",
        InspectionPhase.LaserskoRezanje => "Lasersko rezanje",
        InspectionPhase.HidraulicnoPresanje => "Hidraulično presanje",
        InspectionPhase.ZavrsnaKontrola => "Završna kontrola",
        _ => p.ToString(),
    };
}
