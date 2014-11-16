﻿Option Strict On
Imports System.Threading
Imports AdamSpeight2008.StringFormatDiagnostics.Results
Imports System.Runtime.CompilerServices

Namespace Global.AdamSpeight2008.StringFormatDiagnostics.Analysers

  <HideModuleName>
  Public Module DateTimeSpan

    <Extension>
    Public Function RepCount(sr As StringReader, c As Char) As ParseResult(Of Integer)
      Dim pc = sr.Copy
      Dim res As New ParseResult(Of Integer)(pc, 0)
      While pc.IsNotEoT AndAlso (pc.Value = c)
        res.Value += 1
      End While
      Return res '.LastParse(curr)
    End Function

    Private Function Analyse_Custom_TimeSpan(ct As CancellationToken,
                                                        format As String,
                                                   IndexOffset As Integer,
                                                      Provider As IFormatProvider,
                                                          Args As IEnumerable(Of Object)
                                                ) As Base_Result
      Dim _res_ As New Result(Of String)("")
      If format Is Nothing Then Return _res_ '.AddError(New _Internal.Warning(New ArgumentNullException("format").ToString)) : Return _res_
      '      Dim Curr As IParsedChar = Nothing
      Const _TS_ = "dhmsfF"
      Select Case format.Length
        Case 0
        Case 1 : If _TS_.Contains(format(0)) = False Then _res_.AddError(New Errors.UnknownSpecifier(IndexOffset + 0, format(0)))
        Case 2
          If Not ((format(0) = "%"c) AndAlso _TS_.Contains(format(1))) Then
            _res_.AddError(New Errors.UnknownSpecifier(1, format(1)))
          ElseIf Not ((format(0) = " "c) AndAlso _TS_.Contains(format(1))) Then
            _res_.AddError(New Errors.UnknownSpecifier(1, format(1)))
          ElseIf Not (_TS_.Contains(format(0)) AndAlso (format(1) = " "c)) Then
            _res_.AddError(New Errors.UnknownSpecifier(1, format(1)))
          End If
        Case Else
          Dim _ExitOnFirst_ = False
          Dim Curr As New StringReader(format)
          While Curr.IsNotEoT
            Select Case Curr.Value
              Case "d"c
                Dim reps = Curr.RepCount("d"c)
                Select Case reps.Value
                  Case 0 ' Should never occur
                  Case 1 To 8
                  Case Else
                    _res_.AddError(New Errors.SpecifierUnknown(New String("d"c, reps.Value), IndexOffset + Curr.Index)) ', reps.LastParse.Index))
                End Select
                _res_.IncludeFrom(reps)
                Curr = reps.SR
              Case "h"c
                Dim reps = Curr.RepCount("h"c)
                Select Case reps.Value
                  Case 0, 1, 2
                  Case Else
                    _res_.AddError(New Errors.SpecifierUnknown(New String("h"c, reps.Value), IndexOffset + Curr.Index)) ', reps.LastParse.Index))
                End Select
                _res_.IncludeFrom(reps)
                Curr = reps.SR
              Case "m"c
                Dim reps = Curr.RepCount("m"c)
                Select Case reps.Value
                  Case 0, 1, 2
                  Case Else
                    _res_.AddError(New Errors.SpecifierUnknown(New String("m"c, reps.Value), IndexOffset + Curr.Index)) ', reps.LastParse.Index))
                End Select
                _res_.IncludeFrom(reps)
                Curr = reps.SR
              Case "s"c
                Dim reps = Curr.RepCount("s"c)
                _res_.IncludeFrom(reps)
                Select Case reps.Value
                  Case 0, 1, 2
                  Case Else
                    _res_.AddError(New Errors.SpecifierUnknown(New String("s"c, reps.Value), IndexOffset + Curr.Index)) ', reps.LastParse.Index))
                End Select
                Curr = reps.SR
              Case "f"c
                Dim reps = Curr.RepCount("f"c)
                _res_.IncludeFrom(reps)
                Select Case reps.Value
                  Case 0 To 7
                  Case Else
                    _res_.AddError(New Errors.SpecifierUnknown(New String("f"c, reps.Value), IndexOffset + Curr.Index)) ', reps.LastParse.Index))
                End Select
                Curr = reps.SR
              Case "F"c
                Dim reps = Curr.RepCount("F"c)
                _res_.IncludeFrom(reps)
                Select Case reps.Value
                  Case 0, 1, 2
                  Case Else
                    _res_.AddError(New Errors.SpecifierUnknown(New String("F"c, reps.Value), IndexOffset + Curr.Index)) ', reps.LastParse.Index))
                End Select
                Curr = reps.SR
              Case "'"c
                Dim r = LiteralString(Curr, Curr.Value.Value)
                _res_.IncludeFrom(r)
                If r.Value = False Then Exit While
                Curr = r.SR
              Case "\"c
                If Curr.Peek.HasValue = False Then _res_.AddError(Errors.UnexpectedEoT.Default) : Exit While
                Curr.Next()
                Curr.Next()
              Case Else
                ' NOTE: There is potential for this to be incorrect 
                _res_.AddError(New Errors.UnexpectedChar(IndexOffset + Curr.Index, Curr.Value.Value))
                Exit While
            End Select

          End While
      End Select
      Return _res_ '.LastParse(Curr)
    End Function

    Public Function LiteralString(pc As StringReader, q As Char) As ParseResult(Of Boolean)
      Dim Curr = pc.Copy
      Dim res As New ParseResult(Of Boolean)(Curr, False)
      If pc Is Nothing Then Return res '.AddError(New _Internal.Warning(New ArgumentNullException("pc").ToString))
      While Curr.IsEoT AndAlso res.Value = False
        If Curr.Value = q Then res.Value = True : Exit While
        Curr.Next()
      End While
      If Not res.Value Then res.AddError(Errors.UnexpectedEoT.Default)
      Return res
    End Function

    Public Function Analyse_TimeSpan_ToString(ct As CancellationToken, format As String, IndexOffset As Integer, Provider As IFormatProvider, Args As IEnumerable(Of Object)) As Base_Result
      Dim _res_ As New Result(Of String)("")
      If format Is Nothing Then Return _res_ '.AddError(New _Internal.Warning(New ArgumentNullException("fs").ToString)) : Return _res_
      Dim cf As ICustomFormatter = Nothing
      If Provider IsNot Nothing Then cf = CType(Provider.GetFormat(GetType(ICustomFormatter)), ICustomFormatter)
      If format.Length = 0 Then Return _res_
      If format.Length = 1 Then
        ' Standard TimeSpan Format Strings (http://msdn.microsoft.com/en-us/library/ee372286(v=vs.110)
        If "cgG".Contains(format(0)) Then
          ' Valid specifier
        Else
          _res_.AddError(New Errors.UnknownSpecifier(0 + IndexOffset, format(0)))
        End If
      Else
        ' Custom format string
        _res_.IncludeFrom(Analyse_Custom_TimeSpan(ct, format, IndexOffset, Provider, Args))
      End If
      '    _res_.LastParse = ??
      Return _res_
    End Function

  End Module
End Namespace
