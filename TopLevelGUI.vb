﻿Imports FX3Interface
Imports adisInterface
Imports RegMapClasses
Imports CyUSB
Imports System.IO
Imports System.Reflection
Imports System.Threading
Imports System.Windows.Forms

Public Structure Connection
    Public FX3 As FX3Connection
    Public Dut As AdcmInterfaceBase
    Public RegMap As RegMapCollection
End Structure

Public Class TopLevelGUI
    Private FX3Connected As Boolean
    Private conn As Connection
    Private firmwarePath As String
    Private regMapPath As String

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        Dim firmwarePath As String
        Dim blinkFirmwarePath As String
        Dim regMapPath As String

        'Create a local copy of embedded firmware file
        Dim firmwareResource As String = "FX3Gui.FX3_Firmware.img"
        firmwarePath = System.Reflection.Assembly.GetExecutingAssembly.Location
        firmwarePath = firmwarePath.Substring(0, firmwarePath.LastIndexOf("\") + 1)
        firmwarePath = firmwarePath + "FX3_Firmware.img"
        Dim assembly = System.Reflection.Assembly.GetExecutingAssembly()
        Dim outputStream As New FileStream(firmwarePath, FileMode.Create)
        assembly.GetManifestResourceStream(firmwareResource).CopyTo(outputStream)
        outputStream.Close()

        'Create a local copy of bootloader firmware file
        firmwareResource = "FX3Gui.boot_fw.img"
        blinkFirmwarePath = System.Reflection.Assembly.GetExecutingAssembly.Location
        blinkFirmwarePath = blinkFirmwarePath.Substring(0, blinkFirmwarePath.LastIndexOf("\") + 1)
        blinkFirmwarePath = blinkFirmwarePath + "boot_fw.img"
        assembly = System.Reflection.Assembly.GetExecutingAssembly()
        outputStream = New FileStream(blinkFirmwarePath, FileMode.Create)
        assembly.GetManifestResourceStream(firmwareResource).CopyTo(outputStream)
        outputStream.Close()

        'Create local copy of regmap CSV
        firmwareResource = "FX3Gui.adcmxl3021_regmap_adisAPI.csv"
        regMapPath = System.Reflection.Assembly.GetExecutingAssembly.Location
        regMapPath = regMapPath.Substring(0, regMapPath.LastIndexOf("\") + 1)
        regMapPath = regMapPath + "adcmxl3021_regmap_adisAPI.csv"
        assembly = System.Reflection.Assembly.GetExecutingAssembly()
        outputStream = New FileStream(regMapPath, FileMode.Create)
        assembly.GetManifestResourceStream(firmwareResource).CopyTo(outputStream)
        outputStream.Close()

        'Set connection
        conn.FX3 = New FX3Connection(firmwarePath, blinkFirmwarePath, FX3Connection.DeviceType.ADcmXL)
        conn.RegMap = New RegMapCollection
        conn.RegMap.ReadFromCSV(regMapPath)

        'Add exception handler
        AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf GeneralErrorHandler

        'Seed random number generator
        Randomize()

        'Initialize status box
        StatusText.Text = "Not Connected"
        StatusText.BackColor = Color.Yellow

        'Initialize variables
        FX3Connected = False

        DUTStatusBox.Text = "Waiting For FX3 to Connect"
        DUTStatusBox.BackColor = Color.Yellow

        'Disable buttons initially
        readIDButton.Enabled = FX3Connected
        RegisterAccess.Enabled = FX3Connected
        ManualMode.Enabled = FX3Connected
        configureSPI.Enabled = FX3Connected
        checkConnection.Enabled = FX3Connected
        ReadPinButton.Enabled = FX3Connected
        realTimeStreamButton.Enabled = FX3Connected
        ResetDUTButton.Enabled = FX3Connected
        ResetButton.Enabled = FX3Connected
        TextFileStreamingButton.Enabled = FX3Connected

    End Sub

    Private Sub ConnectButton_Click(sender As Object, e As EventArgs) Handles ConnectButton.Click

        If conn.FX3.FX3BoardAttached Then

            'Get list of connected FX3s and pop up selection window if more than one is detected
            If conn.FX3.DetectedFX3s.Count > 1 Then
                'Create a new instance of the selection form and show the dialog box. Block until the box is closed.
                Dim selectFX3 = New SelectFX3GUI()
                selectFX3.SetConn(conn)
                selectFX3.ShowDialog()
                'Check to make sure the user actually selected a board
                If conn.FX3.ActiveFX3SerialNumber Is Nothing Then
                    MessageBox.Show("Please select an FX3 board to connect to.", "Invalid FX3 selected!", MessageBoxButtons.OK)
                    Exit Sub
                End If
                'Connect to the selected board
                conn.FX3.Connect(conn.FX3.ActiveFX3SerialNumber)
            Else
                'Select the first (and only) board in the list
                conn.FX3.Connect(CType(conn.FX3.DetectedFX3s(0), CyFX3Device).SerialNumber)
            End If

            FX3Connected = conn.FX3.FX3CodeRunningOnTarget
            readIDButton.Enabled = FX3Connected
            RegisterAccess.Enabled = FX3Connected
            ManualMode.Enabled = FX3Connected
            configureSPI.Enabled = FX3Connected
            checkConnection.Enabled = FX3Connected
            ReadPinButton.Enabled = FX3Connected
            realTimeStreamButton.Enabled = FX3Connected
            ResetDUTButton.Enabled = FX3Connected
            TextFileStreamingButton.Enabled = FX3Connected
            ResetButton.Enabled = FX3Connected
            ConnectButton.Enabled = False
            TestDUT()
            If FX3Connected Then
                StatusText.Text = "Connected to FX3"
                StatusText.BackColor = Color.Green
            Else
                StatusText.Text = "Programming FX3 Failed"
                StatusText.BackColor = Color.Red
            End If
        Else
            FX3Connected = False
            StatusText.Text = "ERROR: No FX3 Attached"
            StatusText.BackColor = Color.Red
        End If


    End Sub

    Private Sub TestDUT()
        Dim randomValue As UInteger = CInt(Math.Ceiling(Rnd() * &HFFF)) + 1
        Dim DUTValue As UInteger

        If conn.FX3.PartType = DUTType.ADcmXL3021 Then
            conn.Dut = New adisInterface.AdcmInterface3Axis(conn.FX3)
        ElseIf conn.FX3.PartType = DUTType.ADcmXL2021 Then
            conn.Dut = New adisInterface.AdcmInterface2Axis(conn.FX3)
        ElseIf conn.FX3.PartType = DUTType.ADcmXL1021 Then
            conn.Dut = New adisInterface.AdcmInterface1Axis(conn.FX3)
        End If

        If FX3Connected Then
            conn.Dut.WriteUnsigned(conn.RegMap("USER_SCRATCH"), randomValue)
            DUTValue = conn.Dut.ReadUnsigned(conn.RegMap("USER_SCRATCH"))
            If Not DUTValue = randomValue Then
                DUTStatusBox.Text = "ERROR: DUT Read/Write Failed"
                DUTStatusBox.BackColor = Color.Red
            ElseIf conn.FX3.ReadPin(conn.FX3.DIO2) = 0 Then
                DUTStatusBox.Text = "ERROR: DUT Busy line low"
                DUTStatusBox.BackColor = Color.Red
            Else
                DUTStatusBox.Text = "DUT Connected"
                DUTStatusBox.BackColor = Color.Green
            End If
        End If
    End Sub

    Private Sub RegisterAccess_Click(sender As Object, e As EventArgs) Handles RegisterAccess.Click
        If FX3Connected Then
            Dim regAccess = New registerAccessGUI()
            regAccess.SetConn(conn)
            regAccess.Show()
            Hide()
        Else
            MsgBox("ERROR: FX3 not connected")
        End If
    End Sub

    Private Sub ManualMode_Click(sender As Object, e As EventArgs) Handles ManualMode.Click
        If FX3Connected Then
            Dim manualMode = New manualModeGUI()
            manualMode.SetConn(conn)
            manualMode.Show()
            Hide()
        Else
            MsgBox("ERROR: FX3 not connected")
        End If
    End Sub

    Private Sub ResetButton_Click(sender As Object, e As EventArgs) Handles ResetButton.Click

        conn.FX3.Disconnect()
        FX3Connected = False
        'Disable buttons
        readIDButton.Enabled = FX3Connected
        RegisterAccess.Enabled = FX3Connected
        ManualMode.Enabled = FX3Connected
        configureSPI.Enabled = FX3Connected
        checkConnection.Enabled = FX3Connected
        ReadPinButton.Enabled = FX3Connected
        realTimeStreamButton.Enabled = FX3Connected
        ResetDUTButton.Enabled = FX3Connected
        TextFileStreamingButton.Enabled = FX3Connected
        ConnectButton.Enabled = True
        ResetButton.Enabled = False

        StatusText.Text = "FX3 Reset"
        DUTStatusBox.Text = "Waiting for FX3 to connect"
        StatusText.BackColor = Color.Yellow
        DUTStatusBox.BackColor = Color.Yellow

    End Sub

    Private Sub readIDButton_Click(sender As Object, e As EventArgs) Handles readIDButton.Click
        Dim firmwareID As String
        firmwareID = conn.FX3.GetVersion
        MsgBox(firmwareID)
    End Sub

    Private Sub checkConnection_Click(sender As Object, e As EventArgs) Handles checkConnection.Click
        TestDUT()
    End Sub

    Private Sub configureSPI_Click(sender As Object, e As EventArgs) Handles configureSPI.Click
        If FX3Connected Then
            Dim spiSetup = New SpiSetupGUI()
            SpiSetupGUI.SetConn(conn)
            SpiSetupGUI.Show()
            Hide()
        Else
            MsgBox("ERROR: FX3 not connected")
        End If
    End Sub

    Private Sub ReadPinButton_Click(sender As Object, e As EventArgs) Handles ReadPinButton.Click
        Dim GPIONumber As Integer
        Try
            GPIONumber = Convert.ToInt32(InputBox("Enter GPIO Pin Number: ", "", "0"))
            If GPIONumber > 63 Or GPIONumber < 0 Then
                Throw New IndexOutOfRangeException
            End If
        Catch ex As Exception
            MsgBox("ERROR: Invalid Pin Number")
            Exit Sub
        End Try
        Dim pinValue As UInteger = conn.FX3.ReadPin(New FX3PinObject(GPIONumber))
        MsgBox("Pin " + GPIONumber.ToString + ": " + pinValue.ToString())
    End Sub

    Private Sub realTimeStreamButton_Click(sender As Object, e As EventArgs) Handles realTimeStreamButton.Click
        If FX3Connected Then
            Dim realTimeStream = New RealTimeStreamGUI()
            realTimeStream.SetConn(conn)
            realTimeStream.Show()
            Hide()
        Else
            MsgBox("ERROR: FX3 not connected")
        End If
    End Sub

    Private Sub TextFileStreamingButton_Click(sender As Object, e As EventArgs) Handles TextFileStreamingButton.Click
        If FX3Connected Then
            Dim realTimeStream = New TextFileStreamManagerStreaming()
            realTimeStream.SetConn(conn)
            realTimeStream.Show()
            Hide()
        Else
            MsgBox("ERROR: FX3 not connected")
        End If
    End Sub

    Private Sub ResetDUTButton_Click(sender As Object, e As EventArgs) Handles ResetDUTButton.Click

        conn.FX3.Reset()
        TestDUT()

    End Sub

    Private Sub Cleanup(sender As Object, e As EventArgs) Handles Me.Closing
        If FX3Connected Then
            conn.FX3.Disconnect()
        End If
    End Sub

    Private Sub TopLevelGUI_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Text = "FX3 Evaluation GUI"
    End Sub

    'General exception handeler
    Public Sub GeneralErrorHandler(sender As Object, e As UnhandledExceptionEventArgs)
        FX3Connected = False
        StatusText.Text = "ERROR: Unhandled Exception Occured"
        StatusText.BackColor = Color.Red
        DUTStatusBox.Text = "Waiting for FX3 to connect"
        DUTStatusBox.BackColor = Color.Yellow
    End Sub

End Class