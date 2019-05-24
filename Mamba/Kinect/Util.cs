using System;
using System.Collections.Generic;
using Enklu.Data;
using Enklu.Mamba.Network;
using Microsoft.Kinect;

namespace Enklu.Mamba.Kinect
{
    /// <summary>
    /// Element utilities.
    /// </summary>
    public class Util
    {
        /// <summary>
        /// Creates a BodyElements object, populated with ElementData.
        /// </summary>
        /// <param name="name">Element's name.</param>
        /// <param name="jointMap">Joint asset registration</param>
        /// <returns></returns>
        public static BodyElements CreateBodyElements(string name, Dictionary<JointType, string> jointMap)
        {
            var bodyElements = new BodyElements
            {
                RootElement = new ElementData
                {
                    Schema = new ElementSchemaData
                    {
                        Strings = new Dictionary<string, string> { { "name", name } }
                    },
                    Children = new ElementData[jointMap.Keys.Count]
                }
            };

            var i = 0;
            foreach (var kvp in jointMap)
            {
                var jointElement = new ElementData
                {
                    Type = ElementTypes.Asset,
                    Schema = new ElementSchemaData
                    {
                        Strings = new Dictionary<string, string>
                        {
                            { "name", kvp.Key.ToString() },
                            { "assetSrc", kvp.Value }
                        }
                    }
                };
                bodyElements.RootElement.Children[i++] = jointElement;
                bodyElements.JointElements[kvp.Key] = jointElement;
            }
            
            return bodyElements;
        }
        
        /// <summary>
        /// Finds an element with a matching KinectId.
        /// </summary>
        /// <param name="kinectId"></param>
        /// <param name="element"></param>
        /// <returns></returns>
        public static ElementData FindKinect(string kinectId, ElementData element)
        {
            element.Schema.Strings.TryGetValue("kinect.id", out var schemaId);

            if (schemaId == kinectId)
            {
                return element;
            }
            
            for (int i = 0, len = element.Children.Length; i < len; i++)
            {
                var search = FindKinect(kinectId, element.Children[i]);
                if (search != null)
                {
                    return search;
                }
            }

            return null;
        }
        
        /// <summary>
        /// Determines which joints to track based on schema, and their corresponding assets
        /// </summary>
        /// <param name="elementData"></param>
        /// <returns></returns>
        public static Dictionary<JointType, string> BuildTracking(ElementData elementData)
        {
            const string kinectBodyPrefix = "kinect.body.";
            var prefixLen = kinectBodyPrefix.Length;

            var jointMap = new Dictionary<JointType, string>();
            foreach (var kvp in elementData.Schema.Strings)
            {
                if (kvp.Key.StartsWith(kinectBodyPrefix))
                {
                    var bodyPart = kvp.Key.Substring(prefixLen);
                    bodyPart = bodyPart.Substring(0, bodyPart.IndexOf(".", StringComparison.Ordinal));

                    if (Enum.TryParse<JointType>(bodyPart, true, out var jointType))
                    {
                        jointMap[jointType] = kvp.Value;
                    }
                }
            }

            return jointMap;
        }
    }
}