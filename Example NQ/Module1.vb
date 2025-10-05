Imports System.Net.Sockets
Imports NModbus
Imports System.Threading

Module Module1
    Private Const MASTER_IP As String = "169.254.235.1"
    Private Const MASTER_PORT As Integer = 502
    Private Const UNIT_ID As Byte = 1

    Private Const START_ADDR As UShort = 0
    Private Const END_ADDR As UShort = 199
    Private Const BLOCK_SIZE As UShort = 60

    Private Const CHANGE_THRESHOLD_INT As Integer = 1
    Private Const CHANGE_THRESHOLD_FLOAT As Single = 0.01F

    Private Const SCALE_MM As Double = 0.1

    Private Const FLOAT_MM_ABS_MAX As Double = 2000.0

    Sub Main()
        Console.WriteLine($"IP: {MASTER_IP}:{MASTER_PORT} | UnitId: {UNIT_ID}")
        Console.WriteLine($"Rango: {START_ADDR}..{END_ADDR} (Holding & Input)")
        Console.WriteLine()

        Try
            Using tcp As New TcpClient()
                tcp.ReceiveTimeout = 3000
                tcp.SendTimeout = 3000
                tcp.Connect(MASTER_IP, MASTER_PORT)

                Dim factory = New ModbusFactory()
                Dim master = factory.CreateMaster(tcp)

                Console.WriteLine("Escaneando HOLDING REGISTERS...")
                ScanKind(master, isHolding:=True)

                Console.WriteLine()
                Console.WriteLine("Escaneando INPUT REGISTERS...")
                ScanKind(master, isHolding:=False)
            End Using

        Catch ex As Exception
            Console.WriteLine("ERROR: " & ex.Message)
        End Try

        Console.WriteLine()
        Console.WriteLine("Lectura completada")
        Console.ReadLine()
    End Sub

    Private Sub ScanKind(master As IModbusMaster, isHolding As Boolean)
        Dim kind As String = If(isHolding, "Holding", "Input")
        Dim addr As UShort = START_ADDR

        While addr <= END_ADDR
            Dim count As UShort = CUInt(Math.Min(BLOCK_SIZE, END_ADDR - addr + 1))

            'Primera lectura
            Dim regs1 As UShort() =
                If(isHolding,
                   master.ReadHoldingRegisters(UNIT_ID, addr, count),
                   master.ReadInputRegisters(UNIT_ID, addr, count))

            Thread.Sleep(300)

            'Segunda lectura
            Dim regs2 As UShort() =
                If(isHolding,
                   master.ReadHoldingRegisters(UNIT_ID, addr, count),
                   master.ReadInputRegisters(UNIT_ID, addr, count))

            For i As Integer = 0 To count - 1
                Dim a As UShort = CUShort(addr + i)

                Dim u1 As UShort = regs1(i)
                Dim u2 As UShort = regs2(i)

                'Se actualiza para valores negativos del sensor
                Dim s1 As Short = UShortToInt16(u1)
                Dim s2 As Short = UShortToInt16(u2)

                Dim changedInt As Boolean = Math.Abs(CInt(s2) - CInt(s1)) >= CHANGE_THRESHOLD_INT
                Dim nonZero As Boolean = (s1 <> 0S) OrElse (s2 <> 0S)

                'INT16 escalado (0.1 mm)
                If nonZero OrElse changedInt Then
                    Dim mm1 As Double = s1 * SCALE_MM
                    Dim mm2 As Double = s2 * SCALE_MM
                    If changedInt Then
                        Console.WriteLine($"{kind} @{a}: INT16  {s1}->{s2}  ({mm1:+0.0;-0.0;0.0} -> {mm2:+0.0;-0.0;0.0} mm)")
                    Else
                        Console.WriteLine($"{kind} @{a}: INT16  {s1}  ({mm1:+0.0;-0.0;0.0} mm)")
                    End If
                End If

                'FLOAT32 (par a,a+1)
                If i < count - 1 Then
                    Dim hi1 = regs1(i) : Dim lo1 = regs1(i + 1)
                    Dim hi2 = regs2(i) : Dim lo2 = regs2(i + 1)

                    Dim f1 As Single = ToFloat(hi1, lo1, swapWords:=False)
                    Dim f2 As Single = ToFloat(hi2, lo2, swapWords:=False)
                    If IsFinite(f1) AndAlso IsFinite(f2) Then
                        If Math.Abs(f2 - f1) >= CHANGE_THRESHOLD_FLOAT AndAlso InFloatRangeMm(f1) AndAlso InFloatRangeMm(f2) Then
                            Console.WriteLine($"{kind} @{a}-{a + 1}: FLOAT32 {f1:0.000}->{f2:0.000}")
                        ElseIf InFloatRangeMm(f1) AndAlso Math.Abs(f1) > 0.0001F Then
                            Console.WriteLine($"{kind} @{a}-{a + 1}: FLOAT32 {f1:0.000}")
                        End If
                    End If

                    Dim f1s As Single = ToFloat(hi1, lo1, swapWords:=True)
                    Dim f2s As Single = ToFloat(hi2, lo2, swapWords:=True)
                    If IsFinite(f1s) AndAlso IsFinite(f2s) Then
                        If Math.Abs(f2s - f1s) >= CHANGE_THRESHOLD_FLOAT AndAlso InFloatRangeMm(f1s) AndAlso InFloatRangeMm(f2s) Then
                            Console.WriteLine($"{kind} @{a}-{a + 1}: FLOAT32(sw) {f1s:0.000}->{f2s:0.000}")
                        ElseIf InFloatRangeMm(f1s) AndAlso Math.Abs(f1s) > 0.0001F Then
                            Console.WriteLine($"{kind} @{a}-{a + 1}: FLOAT32(sw) {f1s:0.000}")
                        End If
                    End If
                End If
            Next

            addr = CUShort(addr + count)
        End While
    End Sub
    'Se agrega este nuevo bloque (Para la lectura correctamente)
    Private Function UShortToInt16(u As UShort) As Short
        Return BitConverter.ToInt16(BitConverter.GetBytes(u), 0)
    End Function

    Private Function ToFloat(hi As UShort, lo As UShort, swapWords As Boolean) As Single
        Dim h = hi, l = lo
        If swapWords Then Dim t = h : h = l : l = t
        Dim bytes() As Byte = {
            CByte(l And &HFF),
            CByte((l >> 8) And &HFF),
            CByte(h And &HFF),
            CByte((h >> 8) And &HFF)
        }
        Return BitConverter.ToSingle(bytes, 0)
    End Function

    Private Function IsFinite(x As Single) As Boolean
        Return Not Single.IsNaN(x) AndAlso Not Single.IsInfinity(x)
    End Function

    Private Function InFloatRangeMm(f As Single) As Boolean
        Return Math.Abs(f) <= FLOAT_MM_ABS_MAX
    End Function
End Module
