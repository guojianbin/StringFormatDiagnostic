﻿Option Strict On
Imports System.Collections.Immutable
Imports System.Threading
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.Text
'Imports Roslyn.StringFormatDiagnostics
Imports AdamSpeight2008.StringFormatDiagnostic
Imports AdamSpeight2008.StringFormatDiagnostic.Interfaces
Imports AdamSpeight2008.StringFormatDiagnostic.IssueReports
Imports Common

Namespace Global.AdamSpeight2008.StringFormatDiagnostic.Common
  Public Module Common
    Private _Analysis As New List(Of SFD_Diag)
    Private _IsInitialised As Boolean = False

    Sub Initialise()
      If _IsInitialised Then Exit Sub
      Dim the_file = Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Common.AnalyserList.csv")
      If the_file Is Nothing Then Exit Sub
      Using CSV As New Microsoft.VisualBasic.FileIO.TextFieldParser(the_file) With {.TrimWhiteSpace = True, .Delimiters = {","}, .TextFieldType = FileIO.FieldType.Delimited}
        CSV.CommentTokens = {"//"}
        While CSV.EndOfData = False

          Dim fields = CSV.ReadFields
          If fields.Count < 5 Then Continue While
          Dim indexOfFormatSTring = 0
          If (Integer.TryParse(fields(2), indexOfFormatSTring) = False) OrElse (indexOfFormatSTring < 0) Then Continue While
          Dim sfd As New SFD_Diag(fields(0), fields(1), indexOfFormatSTring, fields(3), fields.Skip(4).ToArray)
          _Analysis.Add(sfd)
        End While
      End Using

    End Sub

    Public ReadOnly Property Analysis() As IEnumerable(Of SFD_Diag)
      Get
        Return _Analysis
      End Get
    End Property

#Region "Constants"

    Public Const DiagnosticId = "String.Format Diagnostic"
    Public Const Description = "Is the formatstring valid?"
    Public Const MessageFormat = "Invalid FormatString (Reason: {0})"
    Public Const Category = "Validation"
#End Region

#Region "Rules"

    Public Rule1 As New DiagnosticDescriptor(id:=DiagnosticId,
                                             description:=Description,
                                             messageFormat:=MessageFormat,
                                             category:=Category,
                                             defaultSeverity:=DiagnosticSeverity.Warning, isEnabledByDefault:=True)
    Public Rule2 As New DiagnosticDescriptor(id:=DiagnosticId,
                                             description:=Description,
                                             messageFormat:="This Constant is used as a FormatString" + Environment.NewLine + MessageFormat,
                                             category:=Category,
                                             defaultSeverity:=DiagnosticSeverity.Error,
                                             isEnabledByDefault:=True)
    Public Rule3 As New DiagnosticDescriptor(id:=DiagnosticId,
                                             description:=Description,
                                             messageFormat:=MessageFormat,
                                             category:=Category,
                                             defaultSeverity:=DiagnosticSeverity.Error, isEnabledByDefault:=True)
#End Region


    Public Function AddWarning(node As SyntaxNode, offset As Integer, endoffset As Integer, ri As IReportIssue) As Diagnostic
      Return Diagnostic.Create(Rule1,
                               Location.Create(node.SyntaxTree, TextSpan.FromBounds(node.SpanStart + offset, node.SpanStart + endoffset)), ri.Message)
    End Function

    Public Function AddError(node As SyntaxNode, offset As Integer, endoffset As Integer, ri As IReportIssue) As Diagnostic
      Return Diagnostic.Create(Rule3,
                               Location.Create(node.SyntaxTree, TextSpan.FromBounds(node.SpanStart + offset, node.SpanStart + endoffset)), ri.Message)
    End Function

    Public Function AddWarningAtSource(node As SyntaxNode, offset As Integer, endoffset As Integer, ri As IReportIssue) As Diagnostic
      Return Diagnostic.Create(Rule2,
                               Location.Create(node.SyntaxTree, TextSpan.FromBounds(node.SpanStart + offset, node.SpanStart + endoffset)), ri.Message)
    End Function

    Public Function AddErrorAtSource(node As SyntaxNode, offset As Integer, endoffset As Integer, ri As IReportIssue) As Diagnostic
      Return Diagnostic.Create(Rule2,
                               Location.Create(node.SyntaxTree, TextSpan.FromBounds(node.SpanStart + offset, node.SpanStart + endoffset)), ri.Message)
    End Function

    Public Function AddInformation(node As SyntaxNode, msg As String) As Diagnostic
      Return Diagnostic.Create(id:=DiagnosticId,
                               category:=Category,
                               message:=msg,
                               severity:=DiagnosticSeverity.Info,
                               isEnabledByDefault:=True,
                               warningLevel:=0,
                               isWarningAsError:=False,
                               location:=Location.Create(node.SyntaxTree, node.Span))
    End Function

#Region "Coomonly used Characters"
    Public Const Opening_Brace As Char = "{"c
    Public Const Closing_Brace As Char = "}"c
    Public Const _SPACE_ As Char = " "c
    Public Const _COMMA_ As Char = ","c
    Public Const _COLON_ As Char = ":"c
    Public Const _MINUS_ As Char = "-"c
    Public Const _QUOTE_ As Char = """"c
#End Region

    Public Const _LIMIT_ As Integer = 1000000  ' This limit is found inside the .net implementation of String.Format.
    Public Const ExitOnFirst = False

    Public Function LiteralString(pc As IParsedChar, q As Char) As OutputResult(Of Boolean)
      Dim res As New OutputResult(Of Boolean)
      If pc Is Nothing Then Return res.AddError(New _Internal.Warning(New ArgumentNullException("pc").ToString))
      Dim curr = pc.Next
      While curr.IsEoT AndAlso res.Output = False
        If curr.Value = q Then res.Output = True : Exit While
        curr = curr.Next
      End While
      If Not res.Output Then res.AddError(Errors.UnexpectedlyReachedEndOfText.Default)
      Return res.LastParse( curr )
    End Function


    Public Function DeString(s As String) As String
      ' If a string is included in double qoutes (") remove the match pair.
      If s Is Nothing Then Return ""
      If (s.Length > 0) AndAlso (s.Last = _QUOTE_) Then s = s.Substring(0, s.Length - 1)
      If (s.Length > 0) AndAlso (s.First = _QUOTE_) Then s = s.Substring(1)
      Return s
    End Function

    Private Function ExponentValue(pc As IParsedChar) As OutputResult(Of Integer)
      Dim _res_ As New OutputResult(Of Integer)
      If pc Is Nothing Then Return _res_.AddError(New _Internal.Warning(New ArgumentNullException("pc").ToString))
      Dim sp = pc
      Dim pr = ParseDigits(sp)
      If pr.Output.Length > 0 Then
        Dim value As Integer 
        If Integer.TryParse(pr.Output, value) Then
          If value.IsBetween(0, 100) Then
            _res_.Output = value
          Else
            _res_.AddError(New Errors.ValueHasExceedLimit("Exponent", value, 99, sp.Index, pr.Last.Index))

          End If
        Else
        End If
      End If
      Return _res_.LastParse(pr.Last )
    End Function

    Private Function Analyse_Custom_Numeric(ct As CancellationToken, format As String, IndexOffset As Integer, Provider As IFormatProvider, Args As IEnumerable(Of Object)) As OutputResult(Of String)
      Dim _res_ As New OutputResult(Of String)
      If format Is Nothing Then Return  _res_.AddError(New _Internal.Warning(New ArgumentNullException("format").ToString))
      Dim _ExitOnFirst_ = False
      Dim s As New TheSourceText(format)
      Dim Curr As IParsedChar = New ParsedChar(s, 0)
      Dim Decimal_Points = 0
      Dim Sections = 1

      While Curr.IsNotEoT
        If ct.IsCancellationRequested Then Exit While
        Select Case Curr.Value
          Case "0"c ' Zero Placeholder
            Curr = Curr.Next
          Case "#"c ' Digit Placeholder
            Curr = Curr.Next
          Case "."c ' Decimal Point
            If Decimal_Points > 0 Then
              _res_.AddError(New Warnings.IgnoredChar(Curr.Value, Curr.Index + IndexOffset))
              If _ExitOnFirst_ Then Exit While
            End If
            Decimal_Points += 1
            Curr = Curr.Next
          Case "%"c ' Percentage Holder
            Curr = Curr.Next
          Case "‰"c ' Per Mille Placeholder
            Curr = Curr.Next
          Case "E"c, "e"c ' Expotential Holder
            Curr = Curr.Next
            If Curr.IsEoT Then _res_.AddError(Errors.UnexpectedlyReachedEndOfText.Default) : Exit Select
            Select Case Curr.Value
              Case "0"c To "9"c
                Dim pr = ExponentValue(Curr)
                _res_.IncludeErrorsFrom(pr)
                Curr = pr.Last
              Case "-"c
                Curr = Curr.Next
                If Curr.IsEoT Then _res_.AddError(Errors.UnexpectedlyReachedEndOfText.Default) : Exit Select
                If Not Curr.IsDigit Then _res_.AddError(New Errors.UnexpectedChar(Curr.Value, IndexOffset + Curr.Index)) : Exit Select
                Dim pr = ExponentValue(Curr)
                _res_.IncludeErrorsFrom(pr)
                Curr = pr.Last
              Case "+"c
                Curr = Curr.Next
                If Curr.IsEoT Then _res_.AddError(Errors.UnexpectedlyReachedEndOfText.Default) : Exit Select
                If Not Curr.IsDigit Then _res_.AddError(New Errors.UnexpectedChar(Curr.Value, IndexOffset + Curr.Index)) : Exit Select
                Dim pr = ExponentValue(Curr)
                _res_.IncludeErrorsFrom(pr)
                Curr = pr.Last
              Case Else
                _res_.AddError(New Errors.UnexpectedChar(Curr.Value, Curr.Index))
            End Select

          Case "'"c, _QUOTE_ ' Literal String Delimiter 
            ' The same character terminates parsing of the literal string eg 'abc'  || "abc"
            Curr = Curr.Next
            While Curr.IsEoT
              If (Curr.Value = "'"c) OrElse (Curr.Value = _QUOTE_) Then Curr = Curr.Next : Exit While
              Curr = Curr.Next
            End While

          Case ";"c ' Group Separator and Number Scaling
            If Sections >= 3 Then _res_.AddError(New Warnings.TooManySections(IndexOffset + Curr.Index)) 
            Sections += 1

            Curr = Curr.Next
          Case "\"c ' Escape Character
            Curr = Curr.Next
            If Curr.IsEoT Then _res_.AddError(Errors.UnexpectedlyReachedEndOfText.Default) : Exit While
            Select Case Curr.Value
              Case "\"c, "0"c, "#"c, "."c, "'"c, _QUOTE_, ";"c, "%"c, "‰"c
                Curr = Curr.Next
              Case Else
                ' To Chek: Could be a parsed error
                Curr = Curr.Next
            End Select
          Case Else ' All other characters
            Curr = Curr.Next
        End Select
      End While
      Return _res_.LastParse(Curr)
    End Function

    Public Function Analyse_Numeric_ToString(ct As CancellationToken, format As String, IndexOffset As Integer, Provider As IFormatProvider, Args As IEnumerable(Of Object)) As OutputResult(Of String)
      Dim _res_ As New OutputResult(Of String)
      If format Is Nothing Then _res_.AddError(New _Internal.Warning(New ArgumentNullException("format").ToString)) : Return _res_
      Dim cf As ICustomFormatter = Nothing
      If Provider IsNot Nothing Then cf = CType(Provider.GetFormat(GetType(ICustomFormatter)), ICustomFormatter)
      If format.Length > 0 Then
        If format.ContainsMoreThan(1, Function(c) Char.IsLetter(c) OrElse Char.IsWhiteSpace(c)) = False Then
          Const _SNFS_ = "CcDdEeFfGgNnPpRrXx"
          Select Case format.Length
            Case 0
            Case 1
              If _SNFS_.Contains(format(0)) Then
                ' ' Parsed as a standard format string.
              Else
                _res_.AddError(New Errors.UnknownSpecifier(format(0), IndexOffset + 0))
              End If
            Case 2
              If _SNFS_.Contains(format(0)) Then
                If format(1).IsDigit Then
                  ' Parsed as a standard format string.
                Else
                  ' Parse as a Custom Numeric format string
                  _res_.IncludeErrorsFrom(Analyse_Custom_Numeric(ct, format, IndexOffset, Provider, Args))
                End If
              Else
                _res_.AddError(New Errors.UnknownSpecifier(format(0), IndexOffset + 0))
              End If
            Case 3
              If _SNFS_.Contains(format(0)) Then
                If format(1).IsDigit Then
                  If format(2).IsDigit Then
                    ' Parsed as a standard format string.
                  Else
                    ' Parse as a Custom Numeric format string
                    _res_.IncludeErrorsFrom(Analyse_Custom_Numeric(ct, format, IndexOffset, Provider, Args))
                  End If
                Else
                  ' Parse as a Custom Numeric format string
                  _res_.IncludeErrorsFrom(Analyse_Custom_Numeric(ct, format, IndexOffset, Provider, Args))
                End If
              Else
                _res_.AddError(New Errors.UnknownSpecifier(format(0), IndexOffset + 0))
              End If
            Case Else
              ' Parse as a Custom Numeric format string
              _res_.IncludeErrorsFrom(Analyse_Custom_Numeric(ct, format, IndexOffset, Provider, Args))
          End Select

        Else
          ' parse custon numeric string.
          _res_.IncludeErrorsFrom(Analyse_Custom_Numeric(ct, format, IndexOffset, Provider, Args))
        End If
      End If
      '   _res_.LastParse = ??
      Return _res_
    End Function

    Private Function Analyse_Custom_DateTime(ct As CancellationToken, format As String, IndexOffset As Integer, Provider As IFormatProvider, Args As IEnumerable(Of Object)) As OutputResult(Of String)
      Dim _res_ As New OutputResult(Of String)
      '_res_.AddError(New Internal_Information("(DateTime) CustomFormatString Diagnostic Not yet Implemented."))
      If format Is Nothing Then Return _res_.AddError(New _Internal.Warning(New ArgumentNullException("format").ToString)) 
      Dim _ExitOnFirst_ = False
      Dim s As New TheSourceText(format)
      Dim Curr As IParsedChar = New ParsedChar(s, IndexOffset + 0)

      While Curr.IsNotEoT
        Select Case Curr.Value
          Case "d"c
            Dim reps = Curr.RepCount("d"c)
            _res_.IncludeErrorsFrom(reps)
            If reps.IsValid = False Then
              Select Case reps.Output
                Case 0 ' Should never occure
                Case 1 To 7
                Case Else
              End Select
              Curr = _res_.Last
            End If
          Case "f"c
            Dim reps = Curr.RepCount("f"c)
            _res_.IncludeErrorsFrom(reps)
            If reps.IsValid = False Then
              Select Case reps.Output
                Case 0 ' Should never occure
                Case 1 To 7
                Case Else
              End Select
              Curr = _res_.Last
            End If
          Case "F"c
            Dim reps = Curr.RepCount("F"c)
            _res_.IncludeErrorsFrom(reps)
            If reps.IsValid = False Then
              Select Case reps.Output
                Case 0 ' Should never occure
                Case 1 To 7
                Case Else
              End Select
              Curr = _res_.Last
            End If
          Case "g"c
            Dim reps = Curr.RepCount("g"c)
            _res_.IncludeErrorsFrom(reps)
            If reps.IsValid = False Then
              Select Case reps.Output
                Case 0 ' Should never occure
                Case 1,2
                Case Else
                  ' Add an error unknown specifier
                  _res_.AddError(New Errors.SpecifierUnknown(New String("g"c, reps.Output), IndexOffset + Curr.Index)) ', reps.LastParse.Index))
              End Select
              Curr = _res_.Last
            End If
          Case "h"c
            Dim reps = Curr.RepCount("h"c)
            _res_.IncludeErrorsFrom(reps)
            If reps.IsValid = False Then
              Select Case reps.Output
                Case 0 ' Should never occure
                Case 1, 2
                Case Else
                  ' Add an error unknown specifier
              End Select
              Curr = _res_.Last
            End If
          Case "H"c
            Dim reps = Curr.RepCount("H"c)
            _res_.IncludeErrorsFrom(reps)
            If reps.IsValid = False Then
              Select Case reps.Output
                Case 0 ' Should never occure
                Case 1, 2
                Case Else
                  ' Add an error unknown specifier
                  _res_.AddError(New Errors.SpecifierUnknown(New String("H"c, reps.Output), IndexOffset + Curr.Index)) 
              End Select
              Curr = _res_.Last
            End If
          Case "K"c
            Dim reps = Curr.RepCount("K"c)
            _res_.IncludeErrorsFrom(reps)
            If reps.IsValid = False Then
              Select Case reps.Output
                Case 0 ' Should never occure
                Case 1, 2
                Case Else
                  ' Add an error unknown specifier
                  _res_.AddError(New Errors.SpecifierUnknown(New String("K"c, reps.Output), IndexOffset + Curr.Index))
              End Select
              Curr = _res_.Last
            End If
          Case "m"c
            Dim reps = Curr.RepCount("m"c)
            _res_.IncludeErrorsFrom(reps)
            If reps.IsValid = False Then
              Select Case reps.Output
                Case 0 ' Should never occure
                Case 1, 2
                Case Else
                  ' Add an error unknown specifier
                  _res_.AddError(New Errors.SpecifierUnknown(New String("m"c, reps.Output), IndexOffset + Curr.Index)) 
              End Select
              Curr = _res_.Last
            End If
          Case "M"c
            Dim reps = Curr.RepCount("M"c)
            _res_.IncludeErrorsFrom(reps)
            If reps.IsValid = False Then
              Select Case reps.Output
                Case 0 ' Should never occure
                Case 1, 2
                Case Else
                  ' Add an error unknown specifier
                  _res_.AddError(New Errors.SpecifierUnknown(New String("M"c, reps.Output), IndexOffset + Curr.Index)) 
              End Select
              Curr = _res_.Last
            End If
          Case "s"c
            Dim reps = Curr.RepCount("s"c)
            _res_.IncludeErrorsFrom(reps)
            If reps.IsValid = False Then
              Select Case reps.Output
                Case 0 ' Should never occure
                Case 1, 2
                Case Else
                  ' Add an error unknown specifier
                  _res_.AddError(New Errors.SpecifierUnknown(New String("s"c, reps.Output), IndexOffset + Curr.Index)) 
              End Select
              Curr = _res_.Last
            End If
          Case "t"c
            Dim reps = Curr.RepCount("t"c)
            _res_.IncludeErrorsFrom(reps)
            If reps.IsValid = False Then
              Select Case reps.Output
                Case 0 ' Should never occure
                Case 1, 2
                Case Else
                  ' Add an error unknown specifier
                  _res_.AddError(New Errors.SpecifierUnknown(New String("t"c, reps.Output), IndexOffset + Curr.Index))
              End Select
              Curr = _res_.Last
            End If
          Case "y"c
            Dim reps = Curr.RepCount("y"c)
            _res_.IncludeErrorsFrom(reps)
            If reps.IsValid = False Then
              Select Case reps.Output
                Case 0 ' Should never occure
                Case 1 To 5
                Case Else
                  ' Add an error unknown specifier
                  _res_.AddError(New Errors.SpecifierUnknown(New String("y"c, reps.Output), IndexOffset + Curr.Index)) 
              End Select
              Curr = _res_.Last
            End If
          Case "z"c
            Dim reps = Curr.RepCount("z"c)
            _res_.IncludeErrorsFrom(reps)
            If reps.IsValid = False Then
              Select Case reps.Output
                Case 0 ' Should never occure
                Case 1,2,3
                Case Else
                  ' Add an error unknown specifier
                  _res_.AddError(New Errors.SpecifierUnknown(New String("z"c, reps.Output), IndexOffset + Curr.Index)) 
              End Select
              Curr = _res_.Last
            End If
          Case ":"c
            Dim reps = Curr.RepCount(":"c)
            _res_.IncludeErrorsFrom(reps)
            If reps.IsValid = False Then
              If reps.Output <> 1 Then _res_.AddError(New Errors.SpecifierUnknown(New String(":"c, reps.Output), IndexOffset + Curr.Index)) 
              Curr = _res_.Last
            End If
          Case "/"c
            Dim reps = Curr.RepCount("/"c)
            _res_.IncludeErrorsFrom(reps)
            If reps.IsValid = False Then
              If reps.Output <> 1 Then _res_.AddError(New Errors.SpecifierUnknown(New String("/"c, reps.Output), IndexOffset + Curr.Index))
              Curr = _res_.Last
            End If
          Case "\"c
            Curr = Curr.Next
            If Curr.IsEoT Then _res_.AddError(Errors.UnexpectedlyReachedEndOfText.Default) : Exit While
            Curr = Curr.Next
          Case "'"c, _QUOTE_
            Dim r = LiteralString(Curr, Curr.Value)
            _res_.IncludeErrorsFrom(r)
            If r.IsValid = False Then Exit While
            Curr = r.Last
          Case "%"c
            Dim nc = Curr.Next
            If nc.IsEoT Then
              _res_.AddError(Errors.UnexpectedlyReachedEndOfText.Default)
            Else
              If "dfFghHKmMstyz:/".Contains(nc.Value) Then
                Curr = nc
              Else
                _res_.AddError(New Errors.UnexpectedChar(nc.Value, IndexOffset + nc.Index))
              End If
            End If
          Case Else
            Curr = Curr.Next
        End Select
      End While
      Return _res_.LastParse(Curr)
    End Function

    Public Function Analyse_DateTime_ToString(ct As CancellationToken, format As String, IndexOffset As Integer, Provider As IFormatProvider, Args As IEnumerable(Of Object)) As OutputResult(Of String)
      Dim _res_ As New OutputResult(Of String)
      If format Is Nothing Then _res_.AddError(New _Internal.Warning(New ArgumentNullException("format").ToString)) : Return _res_
      Dim cf As ICustomFormatter = Nothing
      If Provider IsNot Nothing Then cf = CType(Provider.GetFormat(GetType(ICustomFormatter)), ICustomFormatter)
      If format.Length = 0 Then Return _res_

      If format.Length = 1 Then
        ' Standard Date and Time Format Strings (http://msdn.microsoft.com/en-us/library/az4se3k1(v=vs.110)
        If "dDfFgGmMoOrRstTuUyY".Contains(format(0)) Then
          ' Valid specifier
        Else
          _res_.AddError(New Errors.UnknownSpecifier(format(0), IndexOffset + 0))
        End If
      Else
        ' Custom format string
        _res_.IncludeErrorsFrom(Analyse_Custom_DateTime(ct, format, IndexOffset, Provider, Args))
      End If
      ''    _res_.LastParse = ??
      Return _res_
    End Function

    Private Function Analyse_Custom_TimeSpan(ct As CancellationToken, format As String, IndexOffset As Integer, Provider As IFormatProvider, Args As IEnumerable(Of Object)) As OutputResult(Of String)
      Dim _res_ As New OutputResult(Of String)
      '_res_.AddError(New Internal_Information("(TimeSpan) CustomFormatString Diagnostic Not yet Implemented."))
      If format Is Nothing Then _res_.AddError(New _Internal.Warning(New ArgumentNullException("format").ToString)) : Return _res_
      Dim Curr As IParsedChar = Nothing
      Const _TS_ = "dhmsfF"
      Select Case format.Length
        Case 0
        Case 1 : If _TS_.Contains(format(0)) = False Then _res_.AddError(New Errors.UnknownSpecifier(format(0), IndexOffset + 0))
        Case 2
          If Not ((format(0) = "%"c) AndAlso _TS_.Contains(format(1))) Then
            _res_.AddError(New Errors.UnknownSpecifier(format(1), 1))
          ElseIf Not ((format(0) = " "c) AndAlso _TS_.Contains(format(1))) Then
            _res_.AddError(New Errors.UnknownSpecifier(format(1), 1))
          ElseIf Not (_TS_.Contains(format(0)) AndAlso (format(1) = " "c)) Then
            _res_.AddError(New Errors.UnknownSpecifier(format(1), 1))
          End If
        Case Else
          Dim _ExitOnFirst_ = False
          Dim s As New TheSourceText(format)
          Curr = New ParsedChar(s, 0)
          While Curr.IsNotEoT
            Select Case Curr.Value
              Case "d"c
                Dim reps = Curr.RepCount("d"c)
                _res_.IncludeErrorsFrom(reps)
                If _res_.IsValid Then
                  Select Case reps.Output
                    Case 0 ' Should never occur
                    Case 1 To 8
                    Case Else
                      _res_.AddError(New Errors.SpecifierUnknown(New String("d"c, reps.Output), IndexOffset + Curr.Index)) ', reps.LastParse.Index))
                  End Select
                End If
                Curr = reps.Last
              Case "h"c
                Dim reps = Curr.RepCount("h"c)
                _res_.IncludeErrorsFrom(reps)
                If reps.IsValid Then
                  Select Case reps.Output
                    Case 0, 1, 2
                    Case Else
                      _res_.AddError(New Errors.SpecifierUnknown(New String("h"c, reps.Output), IndexOffset + Curr.Index)) ', reps.LastParse.Index))
                  End Select
                End If
                Curr = reps.Last
              Case "m"c
                Dim reps = Curr.RepCount("m"c)
                _res_.IncludeErrorsFrom(reps)
                If reps.IsValid Then
                  Select Case reps.Output
                    Case 0, 1, 2
                    Case Else
                      _res_.AddError(New Errors.SpecifierUnknown(New String("m"c, reps.Output), IndexOffset + Curr.Index)) ', reps.LastParse.Index))
                  End Select
                End If
                Curr = reps.Last
              Case "s"c
                Dim reps = Curr.RepCount("s"c)
                _res_.IncludeErrorsFrom(reps)
                If reps.IsValid Then
                  Select Case reps.Output
                    Case 0, 1, 2
                    Case Else
                      _res_.AddError(New Errors.SpecifierUnknown(New String("s"c, reps.Output), IndexOffset + Curr.Index)) ', reps.LastParse.Index))
                  End Select
                End If
                Curr = reps.Last
              Case "f"c
                Dim reps = Curr.RepCount("f"c)
                _res_.IncludeErrorsFrom(reps)
                If reps.IsValid Then
                  Select Case reps.Output
                    Case 0 To 7
                    Case Else
                      _res_.AddError(New Errors.SpecifierUnknown(New String("f"c, reps.Output), IndexOffset + Curr.Index)) ', reps.LastParse.Index))
                  End Select
                End If
                Curr = reps.Last
              Case "F"c
                Dim reps = Curr.RepCount("F"c)
                _res_.IncludeErrorsFrom(reps)
                If reps.IsValid Then
                  Select Case reps.Output
                    Case 0, 1, 2
                    Case Else
                      _res_.AddError(New Errors.SpecifierUnknown(New String("F"c, reps.Output), IndexOffset + Curr.Index)) ', reps.LastParse.Index))
                  End Select
                End If
                Curr = reps.Last
              Case "'"c
                Dim r = LiteralString(Curr, Curr.Value)
                _res_.IncludeErrorsFrom(r)
                If r.IsValid = False Then Exit While
                Curr = r.Last
              Case "\"c
                If Curr.Next.IsEoT Then _res_.AddError(Errors.UnexpectedlyReachedEndOfText.Default) : Exit While
                Curr = Curr.Next.Next
              Case Else
                ' NOTE: There is potential for this to be incorrect 
                _res_.AddError(New Errors.UnexpectedChar(Curr.Value, IndexOffset + Curr.Index))
                Exit While
            End Select

          End While
      End Select 
      Return _res_.LastParse(Curr)
    End Function

    Public Function Analyse_TimeSpan_ToString(ct As CancellationToken, format As String, IndexOffset As Integer, Provider As IFormatProvider, Args As IEnumerable(Of Object)) As OutputResult(Of String)
      Dim _res_ As New OutputResult(Of String)
      If format Is Nothing Then _res_.AddError(New _Internal.Warning(New ArgumentNullException("fs").ToString)) : Return _res_

      Dim cf As ICustomFormatter = Nothing
      If Provider IsNot Nothing Then cf = CType(Provider.GetFormat(GetType(ICustomFormatter)), ICustomFormatter)
      If format.Length = 0 Then Return _res_
      If format.Length = 1 Then
        ' Standard TimeSpan Format Strings (http://msdn.microsoft.com/en-us/library/ee372286(v=vs.110)
        If "cgG".Contains(format(0)) Then
          ' Valid specifier
        Else
          _res_.AddError(New Errors.UnknownSpecifier(format(0), 0 + IndexOffset))
        End If
      Else
        ' Custom format string
        _res_.IncludeErrorsFrom(Analyse_Custom_TimeSpan(ct, format, IndexOffset, Provider, Args))
      End If
      '    _res_.LastParse = ??
      Return _res_
    End Function

    Public Function Analyse_DateTimeOffset_ToString(ct As CancellationToken, format As String, IndexOffset As Integer, Provider As IFormatProvider, Args As IEnumerable(Of Object)) As OutputResult(Of String)
      Dim _res_ As New OutputResult(Of String)
      If format Is Nothing Then _res_.AddError(New _Internal.Warning(New ArgumentNullException("fs").ToString)) : Return _res_
      Dim cf As ICustomFormatter = Nothing
      If Provider IsNot Nothing Then cf = CType(Provider.GetFormat(GetType(ICustomFormatter)), ICustomFormatter)
      If format.Length = 0 Then Return _res_
      If format.Length = 1 Then
        ' Standard DateTimeOffset Format Strings (http://msdn.microsoft.com/en-us/library/bb346136(v=vs.110)
        If "cgGKUru".Contains(format(0)) Then
          ' Valid specifier
        Else
          _res_.AddError(New Errors.UnknownSpecifier(format(0), IndexOffset + 0))
        End If
      Else
        ' Custom format string
        _res_.AddError(New _Internal.Information("(DataTimeOffset) CustomFormatString Diagnostic Not yet Implemented."))
      End If
      '    _res_.LastParse = ??
      Return _res_
    End Function

    Public Function Analyse_Enum_ToString(ct As CancellationToken, format As String, IndexOffset As Integer, Provider As IFormatProvider, Args As IEnumerable(Of Object)) As OutputResult(Of String)
      Dim _res_ As New OutputResult(Of String)
      If format Is Nothing Then _res_.AddError(New _Internal.Warning(New ArgumentNullException("fs").ToString)) : Return _res_
      Dim cf As ICustomFormatter = Nothing
      If Provider IsNot Nothing Then cf = CType(Provider.GetFormat(GetType(ICustomFormatter)), ICustomFormatter)

      If format.Length = 0 Then Return _res_
      If format.Length = 1 Then
        ' Standard Enum Format Strings (http://msdn.microsoft.com/en-us/library//c3s1ez6e(v=vs.110)
        If "GgFfDdXx".Contains(format(0)) Then
          ' Valid specifier
          '    _res_.LastParse = ??
          Return _res_
        Else
          _res_.AddError(New Errors.UnknownSpecifier(format(0), IndexOffset + 0))
        End If
      Else
        ' Custom format string
        _res_.AddError(New _Internal.Information("(Enum) CustomFormatString Diagnostic Not yet Implemented."))
      End If
      '    _res_.LastParse = ??
      Return _res_
    End Function

    Public Function ParseValue(pc As IParsedChar, ct As CancellationToken, Limit As Integer, ByRef ParsingIsInAnErrorState As Boolean) As OutputResult(Of Integer)

      Dim _res_ As New OutputResult(Of Integer)
      If pc Is Nothing Then Return _res_
      Do
        If ct.IsCancellationRequested Then Exit Do
        If Not ParsingIsInAnErrorState Then _res_.Output = (10 * _res_.Output) + DigitValue(pc.Value)
        pc = pc.Next
        If pc.IsEoT Then Exit Do
        If Not ParsingIsInAnErrorState AndAlso _res_.Output >= Limit Then ParsingIsInAnErrorState = True
      Loop While IsDigit(pc)
      Return _res_.LastParse(pc)
    End Function

    Public Sub ConsumeSpaces(ByRef pc As IParsedChar, ct As CancellationToken)
      ' Consume spaces
      While (pc IsNot Nothing) AndAlso (Not ct.IsCancellationRequested) AndAlso (pc.Value = _SPACE_)
        pc = pc.Next
      End While
    End Sub

    Private Function SkipSpaces(pc As IParsedChar, ct As CancellationToken) As IParsedChar
      ' Consume spaces
      While (pc IsNot Nothing) AndAlso (Not ct.IsCancellationRequested) AndAlso (pc.Value = _SPACE_)
        pc = pc.Next
      End While
      Return pc
    End Function

    'Private Function IsDigit(c As ParsedChar) As Boolean
    '  Return ("0"c <= c.Value) AndAlso (c.Value <= "9"c)
    'End Function

    Private Function DigitValue(c As Char) As Integer
      Select Case c
        Case "0"c : Return 0
        Case "1"c : Return 1
        Case "2"c : Return 2
        Case "3"c : Return 3
        Case "4"c : Return 4
        Case "5"c : Return 5
        Case "6"c : Return 6
        Case "7"c : Return 7
        Case "8"c : Return 8
        Case "9"c : Return 9
        Case Else
          Return 0
      End Select
    End Function

  End Module
End Namespace



