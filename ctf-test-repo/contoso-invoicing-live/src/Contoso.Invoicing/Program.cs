using Contoso.Invoicing;

var service = new InvoiceService();

var items = new[]
{
    new InvoiceService.LineItem("Widget", 9.99m, 3),
    new InvoiceService.LineItem("Gadget", 24.50m, 1),
};

var total = service.CalculateTotal(items, discountRate: 0.10m, taxRate: 0.19m);
Console.WriteLine($"Invoice total: {total:C}");