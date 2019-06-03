using System;
using System.Collections.Generic;
using Enklu.Data;
using Microsoft.Kinect;

namespace Enklu.Mamba.Kinect
{
    /// <summary>
    /// Defines the status of a <c>BodyElements</c> object.
    /// </summary>
    public enum BodyElementStatus
    {
        InProgress,
        Successful,
        Failure
    }

    /// <summary>
    /// Related Elements for each body.
    /// </summary>
    public class BodyElements
    {
        /// <summary>
        /// The Elements for each joint.
        /// </summary>
        public readonly Dictionary<JointType, ElementData> JointElements = new Dictionary<JointType, ElementData>();
        
        /// <summary>
        /// The root Element all joints position under.
        /// </summary>
        public readonly ElementData RootElement;
        
        /// <summary>
        /// Status of the body.
        /// </summary>
        public readonly BodyElementStatus Status;

        /// <summary>
        /// Only populated if Status is Failure.
        /// </summary>
        public readonly Exception Exception;

        /// <summary>
        /// The current visibility state.
        /// </summary>
        public bool Visible { get; set; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public BodyElements()
        {
            Status = BodyElementStatus.InProgress;
            Visible = true;
        }

        /// <summary>
        /// Success constructor.
        /// </summary>
        /// <inheritdoc />
        public BodyElements(ElementData root) : this()
        {
            Status = BodyElementStatus.Successful;
            RootElement = root;
        }

        /// <summary>
        /// Failure constructor.
        /// </summary>
        /// <inheritdoc />
        public BodyElements(Exception exception) : this()
        {
            Status = BodyElementStatus.Failure;
            Exception = exception;
        }
    }
}