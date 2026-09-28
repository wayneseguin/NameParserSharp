using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NameParser;

namespace NameParseTest
{
    [TestClass]
    public class ConcurrencyTests
    {
        // joinOnConjunctions adds joined pieces to the process-wide Titles/Conjunctions sets while
        // parsing. With plain HashSets, parallel parses corrupted them and every later lookup threw
        // InvalidOperationException ("Operations that change non-concurrent collections ...").
        [TestMethod]
        public void ParallelParsesDoNotCorruptSharedSets()
        {
            var errors = new ConcurrentBag<Exception>();

            Parallel.For(0, 100000, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                try
                {
                    var name = i % 2 == 0 ? "President of Bank" + i + " Smith" : "Mr and Mrs Name" + i + " Jones";
                    _ = new HumanName(name, Prefer.FirstOverPrefix).Last;
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            });

            Assert.IsEmpty(errors, errors.TryPeek(out var first) ? first.Message : null);

            var after = new HumanName("Brill State Bank,", Prefer.FirstOverPrefix);
            Assert.IsFalse(after.IsUnparsable);
        }
    }
}
