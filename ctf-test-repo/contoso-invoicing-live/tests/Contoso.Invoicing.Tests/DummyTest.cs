using Xunit;

namespace Contoso.Invoicing.Tests;

public class DummyTest
{
    [Fact]
    public void ApplyDiscount_HappyPath()
    {
        var sut = new InvoiceService();
        var result = sut.ApplyDiscount(10, 0.5m);
        Assert.Equal(5, result);
    }
    
    [Fact]
    public void ApplyTaxHappyPath()
    {
        var sut = new InvoiceService();
        var result = sut.ApplyTax(10, 0.5m);
        Assert.Equal(15, result);
    }
}
