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
            SchemaVersion = DataSchema.Version;
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

        /// <summary>
        /// Вызывается сериализатором до заполнения полей. Без этого метода новый экземпляр
        /// получал бы текущую версию из конструктора, и файл без поля <c>schemaVersion</c>
        /// выглядел бы как текущий формат. Здесь версия считается неизвестной (0), пока
        /// файл не сообщит иное.
        /// </summary>
        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            SchemaVersion = 0;
        }
    }
}
