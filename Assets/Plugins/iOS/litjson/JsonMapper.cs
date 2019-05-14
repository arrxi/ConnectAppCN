#region Header

using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;

#endregion

namespace LitJson {
    struct PropertyMetadata {
        public MemberInfo Info;
        public bool IsField;
        public Type Type;
    }


    struct ArrayMetadata {
        Type element_type;
        bool is_array;
        bool is_list;

        public Type ElementType {
            get {
                if (this.element_type == null) {
                    return typeof(JsonData);
                }

                return this.element_type;
            }

            set { this.element_type = value; }
        }

        public bool IsArray {
            get { return this.is_array; }
            set { this.is_array = value; }
        }

        public bool IsList {
            get { return this.is_list; }
            set { this.is_list = value; }
        }
    }


    struct ObjectMetadata {
        Type element_type;
        bool is_dictionary;

        IDictionary properties;

        public Type ElementType {
            get {
                if (this.element_type == null) {
                    return typeof(JsonData);
                }

                return this.element_type;
            }

            set { this.element_type = value; }
        }

        public bool IsDictionary {
            get { return this.is_dictionary; }
            set { this.is_dictionary = value; }
        }

        public IDictionary Properties {
            get { return this.properties; }
            set { this.properties = value; }
        }
    }

    /**
     */
    public delegate void ExporterFunc(object obj, JsonWriter writer);

    /**
     */
    public delegate object ImporterFunc(object input);

    /**
     */
    public delegate IJsonWrapper WrapperFactory();

    /**
     * JsonMapper.cs
     *   JSON to .Net object and object to JSON conversions.
     *
     * This file was modified from the original to not use System.Collection.Generics namespace.
     *
     * The authors disclaim copyright to this source code. For more details, see
     * the COPYING file included with this distribution.
     */
    public class JsonMapper {
        #region Fields

        static int max_nesting_depth;

        static IFormatProvider datetime_format;

        static IDictionary base_exporters_table;

        static IDictionary custom_exporters_table;

        static IDictionary base_importers_table;
        static IDictionary custom_importers_table;

        static IDictionary array_metadata;

        static readonly object array_metadata_lock = new object();

        static IDictionary conv_ops;
        static readonly object conv_ops_lock = new object();

        static IDictionary object_metadata;
        static readonly object object_metadata_lock = new object();

        static IDictionary type_properties;
        static readonly object type_properties_lock = new object();

        static JsonWriter static_writer;
        static readonly object static_writer_lock = new object();

        #endregion


        #region Constructors

        static JsonMapper() {
            max_nesting_depth = 100;

            array_metadata = new Hashtable();
            conv_ops = new Hashtable();
            object_metadata = new Hashtable();
            type_properties = new Hashtable();

            static_writer = new JsonWriter();

            datetime_format = DateTimeFormatInfo.InvariantInfo;

            base_exporters_table = new Hashtable();
            custom_exporters_table = new Hashtable();

            base_importers_table = new Hashtable();
            custom_importers_table = new Hashtable();

            RegisterBaseExporters();
            RegisterBaseImporters();
        }

        #endregion


        #region Private Methods

        static void AddArrayMetadata(Type type) {
            if (array_metadata.Contains(type)) {
                return;
            }

            ArrayMetadata data = new ArrayMetadata();
            data.IsArray = type.IsArray;

            if (type.GetInterface("System.Collections.IList") != null) {
                data.IsList = true;
            }

            foreach (PropertyInfo p_info in type.GetProperties()) {
                if (p_info.Name != "Item") {
                    continue;
                }

                ParameterInfo[] parameters = p_info.GetIndexParameters();

                if (parameters.Length != 1) {
                    continue;
                }

                if (parameters[0].ParameterType == typeof(int)) {
                    data.ElementType = p_info.PropertyType;
                }
            }

            lock (array_metadata_lock) {
                try {
                    array_metadata.Add(type, data);
                }
                catch (ArgumentException) {
                    return;
                }
            }
        }

        static void AddObjectMetadata(Type type) {
            if (object_metadata.Contains(type)) {
                return;
            }

            ObjectMetadata data = new ObjectMetadata();

            if (type.GetInterface("System.Collections.IDictionary") != null) {
                data.IsDictionary = true;
            }

            data.Properties = new Hashtable();

            foreach (PropertyInfo p_info in type.GetProperties()) {
                if (p_info.Name == "Item") {
                    ParameterInfo[] parameters = p_info.GetIndexParameters();

                    if (parameters.Length != 1) {
                        continue;
                    }

                    if (parameters[0].ParameterType == typeof(string)) {
                        data.ElementType = p_info.PropertyType;
                    }

                    continue;
                }

                PropertyMetadata p_data = new PropertyMetadata();
                p_data.Info = p_info;
                p_data.Type = p_info.PropertyType;

                data.Properties.Add(p_info.Name, p_data);
            }

            foreach (FieldInfo f_info in type.GetFields()) {
                PropertyMetadata p_data = new PropertyMetadata();
                p_data.Info = f_info;
                p_data.IsField = true;
                p_data.Type = f_info.FieldType;

                data.Properties.Add(f_info.Name, p_data);
            }

            lock (object_metadata_lock) {
                try {
                    object_metadata.Add(type, data);
                }
                catch (ArgumentException) {
                    return;
                }
            }
        }

        static void AddTypeProperties(Type type) {
            if (type_properties.Contains(type)) {
                return;
            }

            IList props = new ArrayList();

            foreach (PropertyInfo p_info in type.GetProperties()) {
                if (p_info.Name == "Item") {
                    continue;
                }

                PropertyMetadata p_data = new PropertyMetadata();
                p_data.Info = p_info;
                p_data.IsField = false;
                props.Add(p_data);
            }

            foreach (FieldInfo f_info in type.GetFields()) {
                PropertyMetadata p_data = new PropertyMetadata();
                p_data.Info = f_info;
                p_data.IsField = true;

                props.Add(p_data);
            }

            lock (type_properties_lock) {
                try {
                    type_properties.Add(type, props);
                }
                catch (ArgumentException) {
                    return;
                }
            }
        }

        static MethodInfo GetConvOp(Type t1, Type t2) {
            lock (conv_ops_lock) {
                if (!conv_ops.Contains(t1)) {
                    conv_ops.Add(t1, new Hashtable());
                }
            }

            if ((conv_ops[t1] as IDictionary).Contains(t2)) {
                return (conv_ops[t1] as IDictionary)[t2] as MethodInfo;
            }

            MethodInfo op = t1.GetMethod("op_Implicit", new Type[] {t2});

            lock (conv_ops_lock) {
                try {
                    (conv_ops[t1] as IDictionary).Add(t2, op);
                }
                catch (ArgumentException) {
                    return (conv_ops[t1] as IDictionary)[t2] as MethodInfo;
                }
            }

            return op;
        }

        static object ReadValue(Type inst_type, JsonReader reader) {
            reader.Read();

            if (reader.Token == JsonToken.ArrayEnd) {
                return null;
            }

            if (reader.Token == JsonToken.Null) {
                if (!inst_type.IsClass) {
                    throw new JsonException(string.Format(
                        "Can't assign null to an instance of type {0}",
                        inst_type));
                }

                return null;
            }

            if (reader.Token == JsonToken.Double ||
                reader.Token == JsonToken.Int ||
                reader.Token == JsonToken.Long ||
                reader.Token == JsonToken.String ||
                reader.Token == JsonToken.Boolean) {
                Type json_type = reader.Value.GetType();

                if (inst_type.IsAssignableFrom(json_type)) {
                    return reader.Value;
                }

                // If there's a custom importer that fits, use it
                if (custom_importers_table.Contains(json_type) &&
                    (custom_importers_table[json_type] as IDictionary).Contains(
                        inst_type)) {
                    ImporterFunc importer =
                        (custom_importers_table[json_type] as IDictionary)[inst_type] as ImporterFunc;

                    return importer(reader.Value);
                }

                // Maybe there's a base importer that works
                if (base_importers_table.Contains(json_type) &&
                    (base_importers_table[json_type] as IDictionary).Contains(inst_type)) {
                    ImporterFunc importer =
                        (base_importers_table[json_type] as IDictionary)[inst_type] as ImporterFunc;

                    return importer(reader.Value);
                }

                // Maybe it's an enum
                if (inst_type.IsEnum) {
                    return Enum.ToObject(inst_type, reader.Value);
                }

                // Try using an implicit conversion operator
                MethodInfo conv_op = GetConvOp(inst_type, json_type);

                if (conv_op != null) {
                    return conv_op.Invoke(null, new object[] {reader.Value});
                }

                // No luck
                throw new JsonException(string.Format(
                    "Can't assign value '{0}' (type {1}) to type {2} :(",
                    reader.Value, json_type, inst_type));
            }

            object instance = null;

            if (reader.Token == JsonToken.ArrayStart) {
                AddArrayMetadata(inst_type);
                ArrayMetadata t_data = (ArrayMetadata) array_metadata[inst_type];

                if (!t_data.IsArray && !t_data.IsList) {
                    throw new JsonException(string.Format(
                        "Type {0} can't act as an array",
                        inst_type));
                }

                IList list;
                Type elem_type;

                if (!t_data.IsArray) {
                    list = (IList) Activator.CreateInstance(inst_type);
                    elem_type = t_data.ElementType;
                }
                else {
                    list = new ArrayList();
                    elem_type = inst_type.GetElementType();
                }

                while (true) {
                    object item = ReadValue(elem_type, reader);
                    if (reader.Token == JsonToken.ArrayEnd) {
                        break;
                    }

                    list.Add(item);
                }

                if (t_data.IsArray) {
                    int n = list.Count;
                    instance = Array.CreateInstance(elem_type, n);

                    for (int i = 0; i < n; i++) {
                        ((Array) instance).SetValue(list[i], i);
                    }
                }
                else {
                    instance = list;
                }
            }
            else if (reader.Token == JsonToken.ObjectStart) {
                AddObjectMetadata(inst_type);
                ObjectMetadata t_data = (ObjectMetadata) object_metadata[inst_type];

                instance = Activator.CreateInstance(inst_type);

                while (true) {
                    reader.Read();

                    if (reader.Token == JsonToken.ObjectEnd) {
                        break;
                    }

                    string property = (string) reader.Value;

                    if (t_data.Properties.Contains(property)) {
                        PropertyMetadata prop_data =
                            (PropertyMetadata) t_data.Properties[property];

                        if (prop_data.IsField) {
                            ((FieldInfo) prop_data.Info).SetValue(
                                instance, ReadValue(prop_data.Type, reader));
                        }
                        else {
                            PropertyInfo p_info =
                                (PropertyInfo) prop_data.Info;

                            if (p_info.CanWrite) {
                                p_info.SetValue(
                                    instance,
                                    ReadValue(prop_data.Type, reader),
                                    null);
                            }
                            else {
                                ReadValue(prop_data.Type, reader);
                            }
                        }
                    }
                    else {
                        if (!t_data.IsDictionary) {
                            throw new JsonException(string.Format(
                                "The type {0} doesn't have the " +
                                "property '{1}'", inst_type, property));
                        }

                        ((IDictionary) instance).Add(
                            property, ReadValue(
                                t_data.ElementType, reader));
                    }
                }
            }

            return instance;
        }

        static IJsonWrapper ReadValue(WrapperFactory factory,
            JsonReader reader) {
            reader.Read();

            if (reader.Token == JsonToken.ArrayEnd ||
                reader.Token == JsonToken.Null) {
                return null;
            }

            IJsonWrapper instance = factory();

            if (reader.Token == JsonToken.String) {
                instance.SetString((string) reader.Value);
                return instance;
            }

            if (reader.Token == JsonToken.Double) {
                instance.SetDouble((double) reader.Value);
                return instance;
            }

            if (reader.Token == JsonToken.Int) {
                instance.SetInt((int) reader.Value);
                return instance;
            }

            if (reader.Token == JsonToken.Long) {
                instance.SetLong((long) reader.Value);
                return instance;
            }

            if (reader.Token == JsonToken.Boolean) {
                instance.SetBoolean((bool) reader.Value);
                return instance;
            }

            if (reader.Token == JsonToken.ArrayStart) {
                instance.SetJsonType(JsonType.Array);

                while (true) {
                    IJsonWrapper item = ReadValue(factory, reader);
                    if (reader.Token == JsonToken.ArrayEnd) {
                        break;
                    }

                    ((IList) instance).Add(item);
                }
            }
            else if (reader.Token == JsonToken.ObjectStart) {
                instance.SetJsonType(JsonType.Object);

                while (true) {
                    reader.Read();

                    if (reader.Token == JsonToken.ObjectEnd) {
                        break;
                    }

                    string property = (string) reader.Value;

                    ((IDictionary) instance)[property] = ReadValue(
                        factory, reader);
                }
            }

            return instance;
        }

        static void RegisterBaseExporters() {
            ExporterFunc func = delegate(object obj, JsonWriter writer) { writer.Write(Convert.ToInt32((byte) obj)); };
            base_exporters_table[typeof(byte)] = func;

            func = delegate(object obj, JsonWriter writer) { writer.Write(Convert.ToString((char) obj)); };
            base_exporters_table[typeof(char)] = func;

            func = delegate(object obj, JsonWriter writer) {
                writer.Write(Convert.ToString((DateTime) obj,
                    datetime_format));
            };
            base_exporters_table[typeof(DateTime)] = func;

            func = delegate(object obj, JsonWriter writer) { writer.Write((decimal) obj); };
            base_exporters_table[typeof(decimal)] = func;

            func = delegate(object obj, JsonWriter writer) { writer.Write(Convert.ToInt32((sbyte) obj)); };
            base_exporters_table[typeof(sbyte)] = func;

            func = delegate(object obj, JsonWriter writer) { writer.Write(Convert.ToInt32((short) obj)); };
            base_exporters_table[typeof(short)] = func;

            func = delegate(object obj, JsonWriter writer) { writer.Write(Convert.ToInt32((ushort) obj)); };
            base_exporters_table[typeof(ushort)] = func;

            func = delegate(object obj, JsonWriter writer) { writer.Write(Convert.ToUInt64((uint) obj)); };
            base_exporters_table[typeof(uint)] = func;

            func = delegate(object obj, JsonWriter writer) { writer.Write((ulong) obj); };
            base_exporters_table[typeof(ulong)] = func;
        }

        static void RegisterBaseImporters() {
            ImporterFunc importer;

            importer = delegate(object input) { return Convert.ToByte((int) input); };
            RegisterImporter(base_importers_table, typeof(int),
                typeof(byte), importer);

            importer = delegate(object input) { return Convert.ToUInt64((int) input); };
            RegisterImporter(base_importers_table, typeof(int),
                typeof(ulong), importer);

            importer = delegate(object input) { return Convert.ToSByte((int) input); };
            RegisterImporter(base_importers_table, typeof(int),
                typeof(sbyte), importer);

            importer = delegate(object input) { return Convert.ToInt16((int) input); };
            RegisterImporter(base_importers_table, typeof(int),
                typeof(short), importer);

            importer = delegate(object input) { return Convert.ToUInt16((int) input); };
            RegisterImporter(base_importers_table, typeof(int),
                typeof(ushort), importer);

            importer = delegate(object input) { return Convert.ToUInt32((int) input); };
            RegisterImporter(base_importers_table, typeof(int),
                typeof(uint), importer);

            importer = delegate(object input) { return Convert.ToSingle((int) input); };
            RegisterImporter(base_importers_table, typeof(int),
                typeof(float), importer);

            importer = delegate(object input) { return Convert.ToDouble((int) input); };
            RegisterImporter(base_importers_table, typeof(int),
                typeof(double), importer);

            importer = delegate(object input) { return Convert.ToDecimal((double) input); };
            RegisterImporter(base_importers_table, typeof(double),
                typeof(decimal), importer);


            importer = delegate(object input) { return Convert.ToUInt32((long) input); };
            RegisterImporter(base_importers_table, typeof(long),
                typeof(uint), importer);

            importer = delegate(object input) { return Convert.ToChar((string) input); };
            RegisterImporter(base_importers_table, typeof(string),
                typeof(char), importer);

            importer = delegate(object input) { return Convert.ToDateTime((string) input, datetime_format); };
            RegisterImporter(base_importers_table, typeof(string),
                typeof(DateTime), importer);
        }

        static void RegisterImporter(IDictionary table, Type json_type, Type value_type, ImporterFunc importer) {
            if (!table.Contains(json_type)) {
                table.Add(json_type, new Hashtable());
            }

            (table[json_type] as IDictionary)[value_type] = importer;
        }

        static void WriteValue(object obj, JsonWriter writer,
            bool writer_is_private,
            int depth) {
            if (depth > max_nesting_depth) {
                throw new JsonException(
                    string.Format("Max allowed object depth reached while " +
                                  "trying to export from type {0}",
                        obj.GetType()));
            }

            if (obj == null) {
                writer.Write(null);
                return;
            }

            if (obj is IJsonWrapper) {
                if (writer_is_private) {
                    writer.TextWriter.Write(((IJsonWrapper) obj).ToJson());
                }
                else {
                    ((IJsonWrapper) obj).ToJson(writer);
                }

                return;
            }

            if (obj is string) {
                writer.Write((string) obj);
                return;
            }

            if (obj is double) {
                writer.Write((double) obj);
                return;
            }

            if (obj is int) {
                writer.Write((int) obj);
                return;
            }

            if (obj is bool) {
                writer.Write((bool) obj);
                return;
            }

            if (obj is long) {
                writer.Write((long) obj);
                return;
            }

            if (obj is Array) {
                writer.WriteArrayStart();

                foreach (object elem in (Array) obj) {
                    WriteValue(elem, writer, writer_is_private, depth + 1);
                }

                writer.WriteArrayEnd();

                return;
            }

            if (obj is IList) {
                writer.WriteArrayStart();
                foreach (object elem in (IList) obj) {
                    WriteValue(elem, writer, writer_is_private, depth + 1);
                }

                writer.WriteArrayEnd();

                return;
            }

            if (obj is IDictionary) {
                writer.WriteObjectStart();
                foreach (DictionaryEntry entry in (IDictionary) obj) {
                    writer.WritePropertyName((string) entry.Key);
                    WriteValue(entry.Value, writer, writer_is_private,
                        depth + 1);
                }

                writer.WriteObjectEnd();

                return;
            }

            Type obj_type = obj.GetType();

            // See if there's a custom exporter for the object
            if (custom_exporters_table.Contains(obj_type)) {
                ExporterFunc exporter = custom_exporters_table[obj_type] as ExporterFunc;
                exporter(obj, writer);

                return;
            }

            // If not, maybe there's a base exporter
            if (base_exporters_table.Contains(obj_type)) {
                ExporterFunc exporter = base_exporters_table[obj_type] as ExporterFunc;
                exporter(obj, writer);

                return;
            }

            // Last option, let's see if it's an enum
            if (obj is Enum) {
                Type e_type = Enum.GetUnderlyingType(obj_type);

                if (e_type == typeof(long)
                    || e_type == typeof(uint)
                    || e_type == typeof(ulong)) {
                    writer.Write((ulong) obj);
                }
                else {
                    writer.Write((int) obj);
                }

                return;
            }

            // Okay, so it looks like the input should be exported as an
            // object
            AddTypeProperties(obj_type);
            IList props = type_properties[obj_type] as IList;

            writer.WriteObjectStart();
            foreach (PropertyMetadata p_data in props) {
                if (p_data.IsField) {
                    writer.WritePropertyName(p_data.Info.Name);
                    WriteValue(((FieldInfo) p_data.Info).GetValue(obj),
                        writer, writer_is_private, depth + 1);
                }
                else {
                    PropertyInfo p_info = (PropertyInfo) p_data.Info;

                    if (p_info.CanRead) {
                        writer.WritePropertyName(p_data.Info.Name);
                        WriteValue(p_info.GetValue(obj, null),
                            writer, writer_is_private, depth + 1);
                    }
                }
            }

            writer.WriteObjectEnd();
        }

        #endregion

        /**
         */
        public static string ToJson(object obj) {
            lock (static_writer_lock) {
                static_writer.Reset();

                WriteValue(obj, static_writer, true, 0);

                return static_writer.ToString();
            }
        }

        /**
         */
        public static void ToJson(object obj, JsonWriter writer) {
            WriteValue(obj, writer, false, 0);
        }

        /**
         */
        public static JsonData ToObject(JsonReader reader) {
            return (JsonData) ToWrapper(
                delegate { return new JsonData(); }, reader);
        }

        /**
         */
        public static JsonData ToObject(TextReader reader) {
            JsonReader json_reader = new JsonReader(reader);

            return (JsonData) ToWrapper(
                delegate { return new JsonData(); }, json_reader);
        }

        /**
         */
        public static JsonData ToObject(string json) {
            return (JsonData) ToWrapper(
                delegate { return new JsonData(); }, json);
        }

        /**
         */
        public static IJsonWrapper ToWrapper(WrapperFactory factory,
            JsonReader reader) {
            return ReadValue(factory, reader);
        }

        /**
         */
        public static IJsonWrapper ToWrapper(WrapperFactory factory,
            string json) {
            JsonReader reader = new JsonReader(json);

            return ReadValue(factory, reader);
        }

        /**
         */
        public static void RegisterExporter(Type tp, ExporterFunc exporter) {
            custom_exporters_table[tp] = exporter;
        }

        /**
         */
        public static void RegisterImporter(Type TJson, Type TValue, ImporterFunc importer) {
            RegisterImporter(custom_importers_table, TJson, TValue, importer);
        }

        /**
         */
        public static void UnregisterExporters() {
            custom_exporters_table.Clear();
        }

        /**
         */
        public static void UnregisterImporters() {
            custom_importers_table.Clear();
        }
    }
}