using System.Collections.Generic;
using UnityEngine;
using YP.Internal;


namespace YP
{
    [CreateAssetMenu(menuName = "YP/Product Catalog", fileName = "Product Catalog")]
    public class ProductCatalog : ScriptableObject
    {
        [field: SerializeField] public List<Product> products { get; private set; }

        public bool ProductExists(string productKey)
        {
            foreach (var product in products)
                if (product.key == productKey) return true;

            return false;
        }


        public Product GetProduct(string productKey)
        {
            foreach (var product in products)
                if (product.key == productKey) return product;

            Core.Error.ProductDoesNotExists("YP Purchases", productKey);
            return null;
        }
    }
}



