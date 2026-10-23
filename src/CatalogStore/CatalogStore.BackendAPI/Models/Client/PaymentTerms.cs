namespace CatalogStore.BackendAPI.Models.Client
{
    public static class PaymentTerms
    {
        public static int Days(credit term) => term switch
        {
            credit.D0 => 0,
            credit.D7 => 7,
            credit.D14 => 14,
            credit.D21 => 21,
            credit.M1 => 30,
            credit.M15 => 45,
            _ => 0
        };
    }
}
