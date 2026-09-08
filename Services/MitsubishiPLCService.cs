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

                plc.ConnectTimeOut = 3000;
                plc.ReceiveTimeOut = 3000;

                var result = plc.ConnectServer();

                _isConnected = result.IsSuccess;

                return _isConnected;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                plc?.ConnectClose();
                plc = null;

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
            if (string.IsNullOrWhiteSpace(address))
                return null;

            address = address
                .Trim()
                .Replace(" ", "")
                .ToUpper();

            if (System.Text.RegularExpressions.Regex.IsMatch(address, @"^[DWMXY]\d+$"))
                return address;

            return null;
        }

        public bool WriteValue(string address, string value)
        {
            lock (_lockObject)
            {
                try
                {
                    if (!CheckPLC())
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
                        if (!short.TryParse(value, out short wordValue))
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

        private bool CheckPLC()
        {
            if (!_isConnected)
                return false;

            if (plc == null)
                return false;

            return true;
        }

        public bool IsConnected()
        {
            return _isConnected;
        }

        public bool SendXYZ(string xReg, string yReg, string zReg, string angleReg,
                            double x, double y, double z, double angle,
                            string confirmReg = null, bool success = false)
        {
            lock (_lockObject)
            {
                if (!CheckPLC())
                    return false;

                try
                {
                    short xValue = (short)Math.Round(x);
                    short yValue = (short)Math.Round(y);
                    short zValue = (short)Math.Round(z);
                    short angleValue = (short)Math.Round(angle);

                    xReg = FormatAddress(xReg);
                    yReg = FormatAddress(yReg);
                    zReg = FormatAddress(zReg);
                    angleReg = FormatAddress(angleReg);

                    if (xReg == null || yReg == null || zReg == null || angleReg == null)
                    {
                        return false;
                    }

                    // Write all values
                    var r1 = plc.Write(xReg, xValue);
                    var r2 = plc.Write(yReg, yValue);
                    var r3 = plc.Write(zReg, zValue);
                    var r4 = plc.Write(angleReg, angleValue);

                    if (!r1.IsSuccess || !r2.IsSuccess || !r3.IsSuccess || !r4.IsSuccess)
                    {
                        _isConnected = false;
                        return false;
                    }

                    // Write confirmation signal if register is provided
                    if (!string.IsNullOrEmpty(confirmReg))
                    {
                        confirmReg = FormatAddress(confirmReg);
                        if (confirmReg != null)
                        {
                            // Write 1 for success, 0 for failure
                            short confirmValue = success ? (short)1 : (short)0;
                            var r5 = plc.Write(confirmReg, confirmValue);

                            // Return false only if confirmation write fails
                            if (!r5.IsSuccess)
                            {
                                Console.WriteLine($"Failed to write confirmation to {confirmReg}: {r5.Message}");
                                return false;
                            }
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SendXYZ Error: {ex.Message}");
                    _isConnected = false;
                    return false;
                }
            }
        }

        // NEW: Method to set confirmation signal only
        public bool SetConfirmation(string confirmReg, bool success)
        {
            lock (_lockObject)
            {
                if (!CheckPLC())
                    return false;

                try
                {
                    confirmReg = FormatAddress(confirmReg);
                    if (string.IsNullOrEmpty(confirmReg))
                        return false;

                    short confirmValue = success ? (short)1 : (short)0;
                    var result = plc.Write(confirmReg, confirmValue);

                    if (!result.IsSuccess)
                    {
                        _isConnected = false;
                        return false;
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SetConfirmation Error: {ex.Message}");
                    _isConnected = false;
                    return false;
                }
            }
        }

        public string ReadValue(string address)
        {
            try
            {
                address = FormatAddress(address);

                if (address == null)
                    return "ERR: Invalid PLC Address";

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
                _isConnected = false;
                return "ERR:" + ex.Message;
            }
        }
    }
}