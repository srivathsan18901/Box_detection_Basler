using HslCommunication;
using HslCommunication.Profinet.Melsec;

namespace VisioNeo_3D.Services
{
    public class MitsubishiPLCService
    {
        private MelsecMcNet plc;
        private readonly object _lockObject = new object();
        private bool _isConnected = false;

        public bool Connect(string ip, int port)
        {
            try
            {
                plc?.ConnectClose();

                plc = new MelsecMcNet(ip, port);

                var result = plc.ConnectServer();

                _isConnected = result.IsSuccess;

                return _isConnected;
            }
            catch
            {
                _isConnected = false;
                return false;
            }
        }

        public void Disconnect()
        {
            plc?.ConnectClose();
            _isConnected = false;
        }

        private string FormatAddress(string address)
        {
            address = address.ToUpper().Trim();

            // Remove any spaces
            address = address.Replace(" ", "");

            // Ensure proper format for different address types
            if (address.StartsWith("D") || address.StartsWith("W"))
            {
                // Word addresses should be like D100, W100
                if (!System.Text.RegularExpressions.Regex.IsMatch(address, @"^[DW]\d+$"))
                {
                    return null;
                }
            }
            else if (address.StartsWith("M") || address.StartsWith("X") || address.StartsWith("Y"))
            {
                // Bit addresses should be like M100, X100, Y100
                if (!System.Text.RegularExpressions.Regex.IsMatch(address, @"^[MXY]\d+$"))
                {
                    return null;
                }
            }

            return address;
        }


        public bool WriteValue(string address, string value)
        {
            lock (_lockObject)
            {
                try
                {
                    if (!IsConnected())
                        return false;

                    address = FormatAddress(address);
                    if (string.IsNullOrEmpty(address))
                        return false;

                    OperateResult result;

                    if (address.StartsWith("M") || address.StartsWith("X") || address.StartsWith("Y"))
                    {
                        bool bitValue = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        result = plc.Write(address, bitValue);
                    }
                    else
                    {
                        if (!ushort.TryParse(value, out ushort wordValue))
                            return false;

                        result = plc.Write(address, wordValue);
                    }

                    return result != null && result.IsSuccess;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Write Error: {ex.Message}");
                    _isConnected = false;
                    return false;
                }
            }
        }

        public bool IsConnected()
        {
            if (!_isConnected)
                return false;

            try
            {
                // Verify connection is still alive
                var test = plc.ReadUInt16("D0");
                if (!test.IsSuccess)
                {
                    _isConnected = false;
                    return false;
                }
                return true;
            }
            catch
            {
                _isConnected = false;
                return false;
            }
        }
        public bool SendXYZ(string xReg, string yReg, string zReg, double x, double y, double z)
        {
            lock (_lockObject)
            {
                if (!IsConnected())
                    return false;

                try
                {
                    short xValue = (short)Math.Round(x);
                    short yValue = (short)Math.Round(y);
                    short zValue = (short)Math.Round(z);

                    var r1 = plc.Write(xReg, xValue);
                    var r2 = plc.Write(yReg, yValue);
                    var r3 = plc.Write(zReg, zValue);

                    return r1.IsSuccess && r2.IsSuccess && r3.IsSuccess;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SendXYZ Error: {ex.Message}");
                    _isConnected = false;
                    return false;
                }
            }
        }


        public string GetStatus()
        {
            if (plc == null)
                return "Disconnected";

            return _isConnected
                ? "Connected"
                : "Disconnected";
        }


        public string ReadValue(string address)
        {
            try
            {
                address = address.ToUpper();

                if (address.StartsWith("M") ||
                    address.StartsWith("X") ||
                    address.StartsWith("Y"))
                {
                    var result = plc.ReadBool(address);

                    if (!result.IsSuccess)
                        return "ERR:" + result.Message;

                    return result.Content ? "1" : "0";
                }

                var read = plc.ReadUInt16(address);

                if (!read.IsSuccess)
                    return "ERR:" + read.Message;

                return read.Content.ToString();
            }
            catch (Exception ex)
            {
                return "ERR:" + ex.Message;
            }
        }
    }


}