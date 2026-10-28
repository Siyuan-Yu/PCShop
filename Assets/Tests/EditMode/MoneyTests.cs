using NUnit.Framework;
using PCShopSell.Core.Common;

namespace PCShopSell.Tests
{
    public class MoneyTests
    {
        [Test]
        public void FromYuan_StoresAsCents()
        {
            Assert.AreEqual(12300, Money.FromYuan(123).Cents);
        }

        [Test]
        public void Arithmetic_Works()
        {
            Money a = Money.FromYuan(10);
            Money b = Money.FromCents(250);
            Assert.AreEqual(1250, (a + b).Cents);
            Assert.AreEqual(750, (a - b).Cents);
            Assert.AreEqual(3000, (a * 3).Cents);
            Assert.AreEqual(-1000, (-a).Cents);
        }

        [Test]
        public void Scale_RoundsToNearestCent()
        {
            // 1000 分 * 0.333 = 333 分
            Assert.AreEqual(333, Money.FromCents(1000).Scale(0.333).Cents);
            // 四舍五入：5 分 * 0.5 = 2.5 -> 3
            Assert.AreEqual(3, Money.FromCents(5).Scale(0.5).Cents);
        }

        [Test]
        public void Comparison_Works()
        {
            Assert.IsTrue(Money.FromYuan(1) < Money.FromYuan(2));
            Assert.IsTrue(Money.FromYuan(2) >= Money.FromYuan(2));
            Assert.AreEqual(Money.FromYuan(5), Money.Max(Money.FromYuan(5), Money.FromYuan(3)));
        }

        [Test]
        public void ToString_FormatsCurrency()
        {
            Assert.AreEqual("¥1,234", Money.FromYuan(1234).ToString());
            Assert.AreEqual("¥12.05", Money.FromCents(1205).ToString());
            Assert.AreEqual("-¥3.50", Money.FromCents(-350).ToString());
        }
    }
}
