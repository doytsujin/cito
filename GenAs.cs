// GenAs.cs - ActionScript code generator
//
// Copyright (C) 2011  Piotr Fusik
//
// This file is part of CiTo, see http://cito.sourceforge.net
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
using System.IO;

namespace Foxoft.Ci
{

public class GenAs : SourceGenerator, ICiSymbolVisitor
{
	string Namespace;

	public GenAs(string namespace_)
	{
		this.Namespace = namespace_;
	}

	void Write(CiCodeDoc doc)
	{
		if (doc == null)
			return;
		// TODO
	}

	void Write(CiVisibility visibility)
	{
		switch (visibility) {
		case CiVisibility.Dead:
		case CiVisibility.Private:
			Write("private ");
			break;
		case CiVisibility.Internal:
			Write("internal ");
			break;
		case CiVisibility.Public:
			Write("public ");
			break;
		}
	}

	void CreateAsFile(CiSymbol symbol)
	{
		CreateFile(Path.Combine(this.OutputPath, symbol.Name + ".as"));
		if (this.Namespace != null) {
			Write("package ");
			WriteLine(this.Namespace);
		}
		else
			WriteLine("package");
		OpenBlock();
		WriteLine("import flash.utils.ByteArray;");
		WriteLine();
		Write(symbol.Documentation);
		Write(symbol.Visibility);
		Write("class ");
		WriteLine(symbol.Name);
		OpenBlock();
	}

	void CloseAsFile()
	{
		CloseBlock(); // class
		CloseBlock(); // package
		CloseFile();
	}

	void ICiSymbolVisitor.Visit(CiEnum enu)
	{
		CreateAsFile(enu);
		for (int i = 0; i < enu.Values.Length; i++) {
			CiEnumValue value = enu.Values[i];
			Write(value.Documentation);
			Write("public static const ");
			WriteUppercaseWithUnderscores(value.Name);
			Write(" : int = ");
			Write(i);
			WriteLine(";");
		}
		CloseAsFile();
	}

	void Write(CiType type)
	{
		Write(" : ");
		if (type is CiStringType)
			Write("String");
		else if (type == CiBoolType.Value)
			Write("Boolean");
		else if (type is CiEnum)
			Write("int");
		else if (type is CiArrayType)
			Write(((CiArrayType) type).ElementType == CiByteType.Value ? "ByteArray" : "Array");
		else
			Write(type.Name);
	}

	bool WriteInit(CiType type)
	{
		CiClassStorageType classType = type as CiClassStorageType;
		if (classType != null) {
			Write(" = new ");
			Write(classType.Class.Name);
			Write("()");
			return true;
		}
		CiArrayStorageType arrayType = type as CiArrayStorageType;
		if (arrayType != null) {
			if (arrayType.ElementType == CiByteType.Value) {
				Write(" = new ByteArray()");
				return true;
			}
			Write(" = new Array(");
			Write(arrayType.Length);
			Write(')');
			return true;
		}
		return false;
	}

	void ICiSymbolVisitor.Visit(CiField field)
	{
		Write(field.Documentation);
		Write(field.Visibility);
		if (field.Type is CiClassStorageType || field.Type is CiArrayStorageType)
			Write("const ");
		else
			Write("var ");
		WriteCamelCase(field.Name);
		Write(field.Type);
		WriteInit(field.Type);
		WriteLine(";");
	}

	void ICiSymbolVisitor.Visit(CiConst konst)
	{
		if (konst.Visibility != CiVisibility.Public)
			return;
		Write(konst.Documentation);
		Write("public static const ");
		WriteUppercaseWithUnderscores(konst.Name);
		Write(konst.Type);
		Write(" = ");
		WriteConst(konst.Value);
		WriteLine(";");
	}

	protected override void WriteConst(object value)
	{
		if (value is CiEnumValue) {
			CiEnumValue ev = (CiEnumValue) value;
			Write(ev.Type.Name);
			Write('.');
			WriteUppercaseWithUnderscores(ev.Name);
		}
		else if (value is Array) {
			Write("[ ");
			WriteContent((Array) value);
			Write(" ]");
		}
		else
			base.WriteConst(value);
	}

	protected override void WriteName(CiConst konst)
	{
		WriteUppercaseWithUnderscores(konst.GlobalName ?? konst.Name);
	}

	protected override int GetPriority(CiExpr expr)
	{
		if (expr is CiPropertyAccess) {
			CiProperty prop = ((CiPropertyAccess) expr).Property;
			if (prop == CiIntType.SByteProperty)
				return 4;
			if (prop == CiIntType.LowByteProperty)
				return 8;
		}
		else if (expr is CiBinaryExpr) {
			if (((CiBinaryExpr) expr).Op == CiToken.Slash)
				return 1;
		}
		return base.GetPriority(expr);
	}

	protected override void Write(CiFieldAccess expr)
	{
		WriteChild(expr, expr.Obj);
		Write('.');
		WriteCamelCase(expr.Field.Name);
	}

	protected override void Write(CiPropertyAccess expr)
	{
		if (expr.Property == CiIntType.SByteProperty) {
			Write('(');
			WriteChild(9, expr.Obj);
			Write(" ^ 128) - 128");
		}
		else if (expr.Property == CiIntType.LowByteProperty) {
			WriteChild(expr, expr.Obj);
			Write(" & 0xff");
		}
		else if (expr.Property == CiStringType.LengthProperty) {
			WriteChild(expr, expr.Obj);
			Write(".length");
		}
		// TODO
		else
			throw new ApplicationException(expr.Property.Name);
	}

	protected override void WriteName(CiMethod method)
	{
		WriteCamelCase(method.Name);
	}

	protected override void Write(CiMethodCall expr)
	{
		if (expr.Method == CiIntType.MulDivMethod) {
			Write("int(");
			WriteChild(3, expr.Obj);
			Write(" * ");
			WriteChild(3, expr.Arguments[0]);
			Write(" / ");
			WriteNonAssocChild(3, expr.Arguments[1]);
			Write(')');
		}
		else if (expr.Method == CiStringType.CharAtMethod) {
			Write(expr.Obj);
			Write(".charCodeAt(");
			Write(expr.Arguments[0]);
			Write(')');
		}
		else if (expr.Method == CiStringType.SubstringMethod) {
			if (expr.Arguments[0].HasSideEffect) {
				Write("Ci.substring(");
				Write(expr.Obj);
				Write(", ");
				Write(expr.Arguments[0]);
				Write(", ");
				Write(expr.Arguments[1]);
				Write(')');
//				this.UsesSubstringMethod = true;
			}
			else {
				Write(expr.Obj);
				Write(".substring(");
				Write(expr.Arguments[0]);
				Write(", ");
				Write(new CiBinaryExpr { Left = expr.Arguments[0], Op = CiToken.Plus, Right = expr.Arguments[1] });
				Write(')');
			}
		}
		else if (expr.Method == CiArrayType.CopyToMethod) {
			Write("Ci.copyArray(");
			Write(expr.Obj);
			Write(", ");
			Write(expr.Arguments[0]);
			Write(", ");
			Write(expr.Arguments[1]);
			Write(", ");
			Write(expr.Arguments[2]);
			Write(", ");
			Write(expr.Arguments[3]);
			Write(')');
//			this.UsesCopyArrayMethod = true;
		}
		else if (expr.Method == CiArrayType.ToStringMethod) {
			Write("Ci.bytesToString(");
			Write(expr.Obj);
			Write(", ");
			Write(expr.Arguments[0]);
			Write(", ");
			Write(expr.Arguments[1]);
			Write(')');
//			this.UsesBytesToStringMethod = true;
		}
		else if (expr.Method == CiArrayStorageType.ClearMethod) {
			Write("Ci.clearArray(");
			Write(expr.Obj);
			Write(')');
//			this.UsesClearArrayMethod = true;
		}
		else
			base.Write(expr);
	}

	protected override void Write(CiBinaryExpr expr)
	{
		if (expr.Op == CiToken.Slash) {
			Write("int(");
			WriteChild(3, expr.Left);
			Write(" / ");
			WriteNonAssocChild(3, expr.Right);
			Write(')');
		}
		else
			base.Write(expr);
	}

	public override void Visit(CiVar stmt)
	{
		Write("var ");
		Write(stmt.Name);
		Write(stmt.Type);
		if (!WriteInit(stmt.Type) && stmt.InitialValue != null) {
			Write(" = ");
			Write(stmt.InitialValue);
		}
	}

	public override void Visit(CiThrow stmt)
	{
		Write("throw ");
		Write(stmt.Message);
		WriteLine(";");
	}

	void ICiSymbolVisitor.Visit(CiMethod method)
	{
		WriteLine();
		if (method.Documentation != null) {
//			WriteDontClose(method.Documentation);
//			foreach (CiParam param in method.Params) {
//				if (param.Documentation != null) {
//					Write(" * @param ");
//					Write(param.Name);
//					Write(' ');
//					Write(param.Documentation.Summary);
//					WriteLine();
//				}
//			}
//			WriteLine(" */");
		}
		Write(method.Visibility);
		if (method.IsStatic)
			Write("static ");
		Write("function ");
		WriteCamelCase(method.Name);
		Write('(');
		bool first = true;
		foreach (CiParam param in method.Params) {
			if (first)
				first = false;
			else
				Write(", ");
			Write(param.Name);
			Write(param.Type);
		}
		Write(")");
		Write(method.ReturnType);
		WriteLine();
		Write(method.Body);
	}

	void ICiSymbolVisitor.Visit(CiClass klass)
	{
		CreateAsFile(klass);
		// TODO: constructor
		foreach (CiSymbol member in klass.Members)
			member.Accept(this);
		foreach (CiConst konst in klass.ConstArrays) {
			Write("private static const ");
			WriteUppercaseWithUnderscores(konst.GlobalName);
			Write(" : Array = ");
			WriteConst(konst.Value);
			WriteLine(";");
		}
		CloseAsFile();
	}

	public override void Write(CiProgram prog)
	{
		foreach (CiSymbol symbol in prog.Globals) {
			symbol.Accept(this);
		}
	}
}

}