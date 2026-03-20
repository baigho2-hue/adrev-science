using Plugin.InAppBilling;

namespace AdRev.Mobile.Services;

public class BillingService
{
    private const string PRO_LICENCE_SKU = "adrev_collect_pro_unlock";

    public async Task<bool> IsProUserAsync()
    {
        // Check local storage/preferences first for speed
        if (Preferences.Get("IsProLicenceActive", false))
            return true;

        // Verify with Google Play to be "honest"
        try
        {
            var billing = CrossInAppBilling.Current;
            if (!await billing.ConnectAsync())
                return false;

            var purchases = await billing.GetPurchasesAsync(ItemType.InAppPurchase);
            var hasPro = purchases?.Any(p => p.ProductId == PRO_LICENCE_SKU && p.State == PurchaseState.Purchased) ?? false;

            if (hasPro)
                Preferences.Set("IsProLicenceActive", true);

            return hasPro;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Billing Check Error: {ex.Message}");
            return Preferences.Get("IsProLicenceActive", false);
        }
        finally
        {
            await CrossInAppBilling.Current.DisconnectAsync();
        }
    }

    public async Task<bool> PurchaseProLicenceAsync()
    {
        try
        {
            var billing = CrossInAppBilling.Current;
            if (!await billing.ConnectAsync())
                return false;

            var purchase = await billing.PurchaseAsync(PRO_LICENCE_SKU, ItemType.InAppPurchase);

            if (purchase != null && purchase.State == PurchaseState.Purchased)
            {
                // Verify and Acknowledge the purchase (Google requirement)
                await billing.FinalizePurchaseAsync(purchase.TransactionIdentifier);
                
                Preferences.Set("IsProLicenceActive", true);
                return true;
            }
        }
        catch (InAppBillingPurchaseException purchaseEx)
        {
            System.Diagnostics.Debug.WriteLine($"Purchase Error: {purchaseEx.PurchaseError}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Billing Error: {ex.Message}");
        }
        finally
        {
            await CrossInAppBilling.Current.DisconnectAsync();
        }

        return false;
    }

    public async Task RestorePurchasesAsync()
    {
        await IsProUserAsync();
    }
}
