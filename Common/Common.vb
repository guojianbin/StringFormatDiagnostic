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
Imports System.Runtime.CompilerServices

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
      Return res.LastParse(curr)
    End Function


    Public Function DeString(s As String) As String
      ' If a string is included in double qoutes (") remove the match pair.
      If s Is Nothing Then Return ""
      If (s.Length > 0) AndAlso (s.Last = _QUOTE_) Then s = s.Substring(0, s.Length - 1)
      If (s.Length > 0) AndAlso (s.First = _QUOTE_) Then s = s.Substring(1)
      Return s
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
    <Extension>
    Public Function SkipSpaces(pc As IParsedChar, ct As CancellationToken) As IParsedChar
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



