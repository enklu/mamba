using System.Collections.Generic;
using Enklu.Data;
using Enklu.Mamba.Network;

namespace Enklu.Mamba.Kinect
{
    /// <summary>
    /// Element utilities.
    /// </summary>
    public class Util
    {
        /// <summary>
        /// Creates a default element with a given name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static ElementData CreateElementData(string name)
        {
            return new ElementData
            {
                Schema = new ElementSchemaData
                {
                    Strings = new Dictionary<string, string>
                    {
                        { "name", name }
                    }
                }
            };
        }

        /// <summary>
        /// Creates an asset with a given name and assetSrc.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="asset"></param>
        /// <returns></returns>
        public static ElementData CreateElementData(string name, string asset)
        {
            var elm = CreateElementData(name);
            elm.Schema.Strings["assetSrc"] = asset;
            elm.Type = ElementTypes.ASSET;
            return elm;
        }
    }
}