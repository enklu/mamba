using System.Collections.Generic;
using Enklu.Data;
using Microsoft.Kinect;

namespace Enklu.Mamba.Kinect
{
    /// <summary>
    /// Related Elements for each body.
    /// </summary>
    public class BodyElements
    {
        /// <summary>
        /// The root Element all joints position under.
        /// </summary>
        public ElementData RootElement;
            
        /// <summary>
        /// The Elements for each joint.
        /// </summary>
        public readonly Dictionary<JointType, ElementData> JointElements = new Dictionary<JointType, ElementData>();
            
        /// <summary>
        /// The current visibility state.
        /// </summary>
        public bool Visible = true;
    }
}