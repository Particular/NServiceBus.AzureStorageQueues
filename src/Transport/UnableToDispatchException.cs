namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class UnableToDispatchException : Exception
    {
        public UnableToDispatchException(Exception ex)
            : base(ExceptionMessage, ex)
        {
        }

        protected UnableToDispatchException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Queue = info.GetString(nameof(Queue));
            Namespace = info.GetString(nameof(Namespace));
        }

        /// <summary>
        /// The queue name dispatch was sending a message to.
        /// </summary>
        public string Queue { get; set; }

        /// <summary>
        /// The queue namemespace dispatch was sending a message to.
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// Gets the object data for serialization purposes.
        /// </summary>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(Queue), Queue);
            info.AddValue(nameof(Namespace), Namespace);
        }

        public const string ExceptionMessage = "Message couldn't be dispatched";
    }
}