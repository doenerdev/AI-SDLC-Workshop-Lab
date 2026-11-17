namespace Contoso.Invoicing;

public sealed class InvoiceService
{
    public sealed record LineItem(string Description, decimal UnitPrice, int Quantity);
    
    public decimal CalculateSubtotal(IEnumerable<LineItem> items)
    {
        decimal subtotal = 0m;
        foreach (var item in items)
        {
            subtotal += item.UnitPrice * item.Quantity;
        }
        return subtotal;
    }

    public decimal ApplyDiscount(decimal subtotal, decimal discountRate)
    {
        return subtotal * (1 - discountRate);
    }

    public decimal ApplyTax(decimal amount, decimal taxRate)
    {
        return amount * (1 + taxRate);
    }

    public decimal CalculateTotal(IEnumerable<LineItem> items, decimal discountRate, decimal taxRate)
    {
        var subtotal = CalculateSubtotal(items);
        var discounted = ApplyDiscount(subtotal, discountRate);
        return ApplyTax(discounted, taxRate);
    }
}
