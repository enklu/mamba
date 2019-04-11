namespace Enklu.Mamba.Network
{
    /// <summary>
    /// Configuration required for Mycelium connection.
    /// </summary>
    public class MyceliumControllerConfiguration
    {
        /// <summary>
        /// IP to connect to.
        /// </summary>
        public string Ip { get; set; }

        /// <summary>
        /// Port to connect to.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Token to use for connection.
        /// </summary>
        public string Token { get; set; }
    }
}