﻿'Option Strict On
Imports System.Linq.ImmutableArrayExtensions
Imports System.Threading
Imports Microsoft.CodeAnalysis

Imports AdamSpeight2008.StringFormatDiagnostics.Results
Imports AdamSpeight2008.StringFormatDiagnostics.Errors
Namespace Global.AdamSpeight2008.StringFormatDiagnostics.Common

  Public Class Arg_Format
    Inherits Arg_Base
    Public ReadOnly Property Format As String
    Public Sub New(Span As IndexSpan?, Format As String)
      MyBase.New(Span)
      _Format = Format
    End Sub
    Public Overrides Function ToString() As String
      Return String.Format("{0}{1}", Format, MyBase.ToString)
    End Function
  End Class

End Namespace
