// GenCl.cs - OpenCL C code generator
//
// Copyright (C) 2020  Piotr Fusik
//
// This file is part of CiTo, see https://github.com/pfusik/cito
//
// CiTo is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// CiTo is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with CiTo.  If not, see http://www.gnu.org/licenses/

using System;

namespace Foxoft.Ci
{

public class GenCl : GenC
{
	bool StringEquals;
	bool StringLength;

	protected override void IncludeStdBool()
	{
	}

	protected override void IncludeMath()
	{
	}

	protected override void Write(TypeCode typeCode)
	{
		switch (typeCode) {
		case TypeCode.SByte:
			Write("char");
			break;
		case TypeCode.Byte:
			Write("uchar");
			break;
		case TypeCode.Int16:
			Write("short");
			break;
		case TypeCode.UInt16:
			Write("ushort");
			break;
		case TypeCode.Int32:
			Write("int");
			break;
		case TypeCode.Int64:
			Write("long");
			break;
		default:
			throw new NotImplementedException(typeCode.ToString());
		}
	}

	protected override void WriteStringPtrType()
	{
		Write("constant char *");
	}

	public override CiExpr Visit(CiInterpolatedString expr, CiPriority parent)
	{
		throw new NotImplementedException("Interpolated strings not supported in OpenCL C");
	}

	protected override void WriteCamelCaseNotKeyword(string name)
	{
		switch (name) {
		case "Constant":
		case "Global":
		case "Local":
		case "Private":
		case "constant":
		case "global":
		case "local":
		case "private":
			WriteCamelCase(name);
			Write('_');
			break;
		default:
			base.WriteCamelCaseNotKeyword(name);
			break;
		}
	}
		
	protected override void WriteEqual(CiBinaryExpr expr, CiPriority parent, bool not)
	{
		if ((expr.Left.Type is CiStringType && expr.Right.Type != CiSystem.NullType)
		 || (expr.Right.Type is CiStringType && expr.Left.Type != CiSystem.NullType)) {
			this.StringEquals = true;
			if (not)
				Write('!');
			Write("streq(");
			expr.Left.Accept(this, CiPriority.Statement);
			Write(", ");
			expr.Right.Accept(this, CiPriority.Statement);
			Write(')');
		}
		else
			base.WriteEqual(expr, parent, not);
	}

	protected override void WriteStringLength(CiExpr expr)
	{
		this.StringLength = true;
		Write("strlen(");
		expr.Accept(this, CiPriority.Statement);
		Write(')');
	}

	void WriteConsoleWrite(CiExpr[] args, bool newLine)
	{
		Write("printf(");
		if (args.Length == 0)
			Write("\"\\n\")");
		else if (args[0] is CiInterpolatedString interpolated)
			WritePrintf(interpolated, newLine);
		else {
			Write("\"%");
			Write(args[0].Type is CiIntegerType ? 'd' : args[0].Type is CiFloatingType ? 'g' : 's');
			if (newLine)
				Write("\\n");
			Write("\", ");
			args[0].Accept(this, CiPriority.Statement);
			Write(')');
		}
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, CiExpr[] args, CiPriority parent)
	{
		if (obj == null)
			WriteCCall(null, method, args);
		else if (obj.IsReferenceTo(CiSystem.MathClass))
			WriteMathCall(method, args);
		else if (method == CiSystem.ConsoleWrite)
			WriteConsoleWrite(args, false);
		else if (method == CiSystem.ConsoleWriteLine)
			WriteConsoleWrite(args, true);
		else
			WriteCCall(obj, method, args);
	}

	void WriteLibrary()
	{
		if (this.StringEquals) {
			WriteLine();
			WriteLine("static bool streq(constant char *str1, constant char *str2)");
			OpenBlock();
			WriteLine("for (size_t i = 0; str1[i] == str2[i]; i++) {");
			WriteLine("\tif (str1[i] == '\\0')");
			WriteLine("\t\treturn true;");
			WriteLine('}');
			WriteLine("return false;");
			CloseBlock();
		}
		if (this.StringLength) {
			WriteLine();
			WriteLine("static int strlen(constant char *str)");
			OpenBlock();
			WriteLine("int len = 0;");
			WriteLine("while (str[len] != '\\0')");
			WriteLine("\tlen++;");
			WriteLine("return len;");
			CloseBlock();
		}
	}

	public override void Write(CiProgram program)
	{
		this.WrittenClasses.Clear();
		this.StringEquals = false;
		this.StringLength = false;
		OpenStringWriter();
		foreach (CiClass klass in program.Classes) {
			this.CurrentClass = klass;
			WriteConstructor(klass);
			WriteDestructor(klass);
			foreach (CiMethod method in klass.Methods)
				Write(klass, method);
		}

		CreateFile(this.OutputFile);
		WriteTopLevelNatives(program);
		WriteTypedefs(program, true);
		foreach (CiClass klass in program.Classes)
			WriteSignatures(klass, true);
		WriteTypedefs(program, false);
		foreach (CiClass klass in program.Classes)
			WriteStruct(klass);
		WriteResources(program.Resources);
		WriteLibrary();
		CloseStringWriter();
		CloseFile();
	}
}

}

