using Doggiehood.Core.Dogs;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Graybox coat colors per breed (#35) until real breed models land —
    /// each breed gets a distinct color so no two breeds read the same.
    /// Easter-egg coat overrides (#68) map on top.
    /// </summary>
    public static class BreedCoats
    {
        public static Color ForDog(Dog dog)
        {
            switch (dog.Coat)
            {
                case CoatColor.Black: return CoreColors.FromHex("#2B2B2B");
                case CoatColor.Light: return CoreColors.FromHex("#F4E3B2");
                case CoatColor.Dark: return CoreColors.FromHex("#8A6A1F");
            }

            switch (dog.Breed)
            {
                case Breed.GermanShepherd: return CoreColors.FromHex("#8C6239");
                case Breed.GoldenRetriever: return CoreColors.FromHex("#E8B84B");
                case Breed.Labrador: return CoreColors.FromHex("#EDD9A3");
                case Breed.Beagle: return CoreColors.FromHex("#B5651D");
                case Breed.Chihuahua: return CoreColors.FromHex("#D9A066");
                case Breed.FrenchBulldog: return CoreColors.FromHex("#A3A3A3");
                case Breed.Puggle: return CoreColors.FromHex("#C9A227");
                case Breed.Frenchton: return CoreColors.FromHex("#6E6E6E");
                default: return Color.white;
            }
        }
    }
}
