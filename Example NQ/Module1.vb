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
            Dim regs1 As UShort()
            If isHolding Then
                regs1 = master.ReadHoldingRegisters(UNIT_ID, addr, count)
            Else
                regs1 = master.ReadInputRegisters(UNIT_ID, addr, count)
            End If

            'Pequeña espera y segunda lectura
            Thread.Sleep(300)

            Dim regs2 As UShort()
            If isHolding Then
                regs2 = master.ReadHoldingRegisters(UNIT_ID, addr, count)
            Else
                regs2 = master.ReadInputRegisters(UNIT_ID, addr, count)
            End If

            'Analiza cada dirección del bloque
            For i As Integer = 0 To count - 1
                Dim a As UShort = CUShort(addr + i)
                Dim v1 As UShort = regs1(i)
                Dim v2 As UShort = regs2(i)

                Dim changedInt As Boolean = Math.Abs(CInt(v2) - CInt(v1)) >= CHANGE_THRESHOLD_INT
                Dim nonZero As Boolean = (v1 <> 0) OrElse (v2 <> 0)

                'Int16 escalado (0.1 mm)
                If nonZero OrElse changedInt Then
                    Dim mm1 As Double = CDbl(v1) / 10.0
                    Dim mm2 As Double = CDbl(v2) / 10.0
                    If changedInt Then
                        Console.WriteLine($"{kind} @{a}: INT16  {v1}->{v2}  (~{mm1:0.0}->{mm2:0.0} mm)")
                    ElseIf nonZero Then
                        Console.WriteLine($"{kind} @{a}: INT16  {v1}  (~{mm1:0.0} mm)")
                    End If
                End If

                'FLOAT32 (se usa par a,a+1)
                If i < count - 1 Then
                    Dim hi1 = regs1(i)
                    Dim lo1 = regs1(i + 1)
                    Dim hi2 = regs2(i)
                    Dim lo2 = regs2(i + 1)

                    'Lectura numero 1
                    Dim f1 As Single = ToFloat(hi1, lo1, swapWords:=False)
                    Dim f2 As Single = ToFloat(hi2, lo2, swapWords:=False)
                    If Math.Abs(f2 - f1) >= CHANGE_THRESHOLD_FLOAT AndAlso IsFinite(f1) AndAlso IsFinite(f2) Then
                        Console.WriteLine($"{kind} @{a}-{a + 1}: FLOAT32 {f1:0.000}->{f2:0.000}")
                    ElseIf (IsFinite(f1) AndAlso Math.Abs(f1) > 0.0001F) Then
                        ' Muestra valores no-cero que podrían ser PV
                        Console.WriteLine($"{kind} @{a}-{a + 1}: FLOAT32 {f1:0.000}")
                    End If

                    'Lectura numero 2
                    Dim f1s As Single = ToFloat(hi1, lo1, swapWords:=True)
                    Dim f2s As Single = ToFloat(hi2, lo2, swapWords:=True)
                    If Math.Abs(f2s - f1s) >= CHANGE_THRESHOLD_FLOAT AndAlso IsFinite(f1s) AndAlso IsFinite(f2s) Then
                        Console.WriteLine($"{kind} @{a}-{a + 1}: FLOAT32(sw) {f1s:0.000}->{f2s:0.000}")
                    ElseIf (IsFinite(f1s) AndAlso Math.Abs(f1s) > 0.0001F) Then
                        Console.WriteLine($"{kind} @{a}-{a + 1}: FLOAT32(sw) {f1s:0.000}")
                    End If
                End If
            Next

            addr = CUShort(addr + count)
        End While
    End Sub

    Private Function ToFloat(hi As UShort, lo As UShort, swapWords As Boolean) As Single
        Dim h = hi
        Dim l = lo
        If swapWords Then
            Dim t = h : h = l : l = t
        End If
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

End Module
