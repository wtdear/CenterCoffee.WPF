using System.Collections.Generic;
using System.Runtime.Serialization;
using CenterCoffee.Domain.Warehouse;

namespace CenterCoffee.Infrastructure.Persistence
{
    /// <summary>
    /// Корневой документ локальной базы. Списки создаются пустыми:
    /// приложение не наполняет базу демонстрационными данными.
    /// </summary>
    [DataContract]
    public sealed class AppData
    {
        [DataMember(Name = "schemaVersion")]
        public int SchemaVersion { get; set; }

        [DataMember(Name = "ingredients")]
        public List<Ingredient> Ingredients { get; set; }

        [DataMember(Name = "stockMovements")]
        public List<StockMovement> Movements { get; set; }

        public AppData()
        {
            SchemaVersion = 1;
            Ingredients = new List<Ingredient>();
            Movements = new List<StockMovement>();
        }

        public void EnsureCollections()
        {
            if (Ingredients == null)
            {
                Ingredients = new List<Ingredient>();
            }

            if (Movements == null)
            {
                Movements = new List<StockMovement>();
            }
        }
    }
}
