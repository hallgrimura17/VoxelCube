using System.Collections.Generic;
using System.Linq;

namespace Assets.Extensions
{
    public static class IEnumerableExtensions
    {
        public static T UnseededRandomElement<T>(this List<T> enumerable)
        {
            if (enumerable == null || !enumerable.Any())
                return default;
            return enumerable[UnityEngine.Random.Range(0, enumerable.Count())];
        }


        public static T NextElement<T>(this List<T> enumerable)
        {
            var element = enumerable.FirstOrDefault();
            if (element != null)
                enumerable.Remove(element);
            return element;
        }
    }
}