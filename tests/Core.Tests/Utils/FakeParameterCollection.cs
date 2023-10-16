using System.Collections;
using System.Data.Common;

namespace SqlBuildingBlocks.Core.Tests.Utils;

public class FakeParameterCollection : DbParameterCollection
{
    private ArrayList _parameters = new ArrayList();

    public override int Count => _parameters.Count;

    public override object SyncRoot => ((ICollection)_parameters).SyncRoot;

    public override int Add(object value)
    {
        _parameters.Add(value);
        return _parameters.Count - 1;
    }

    public override void Clear()
    {
        _parameters.Clear();
    }

    public override bool Contains(object value)
    {
        return _parameters.Contains(value);
    }

    public override bool Contains(string value)
    {
        foreach (DbParameter parameter in _parameters)
        {
            if (parameter.ParameterName == value)
            {
                return true;
            }
        }

        return false;
    }

    public override void CopyTo(Array array, int index)
    {
        _parameters.CopyTo(array, index);
    }

    public override IEnumerator GetEnumerator()
    {
        return _parameters.GetEnumerator();
    }

    public override int IndexOf(string parameterName)
    {
        for (int i = 0; i < _parameters.Count; i++)
        {
            DbParameter parameter = (DbParameter)_parameters[i];
            if (parameter.ParameterName == parameterName)
            {
                return i;
            }
        }

        return -1;
    }

    public override int IndexOf(object value)
    {
        return _parameters.IndexOf(value);
    }

    public override void Insert(int index, object value)
    {
        _parameters.Insert(index, value);
    }

    public override void Remove(object value)
    {
        _parameters.Remove(value);
    }

    public override void RemoveAt(int index)
    {
        _parameters.RemoveAt(index);
    }

    public override void RemoveAt(string parameterName)
    {
        int index = IndexOf(parameterName);
        if (index != -1)
        {
            _parameters.RemoveAt(index);
        }
    }

    public override void AddRange(Array values)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        foreach (object value in values)
        {
            if (!(value is DbParameter))
            {
                throw new InvalidCastException("All items in values must be of type DbParameter");
            }

            _parameters.Add(value);
        }
    }

    protected override DbParameter GetParameter(int index)
    {
        return (DbParameter)_parameters[index];
    }

    protected override DbParameter GetParameter(string parameterName)
    {
        int index = IndexOf(parameterName);
        if (index != -1)
        {
            return (DbParameter)_parameters[index];
        }

        return null;
    }

    protected override void SetParameter(int index, DbParameter value)
    {
        _parameters[index] = value;
    }

    protected override void SetParameter(string parameterName, DbParameter value)
    {
        int index = IndexOf(parameterName);
        if (index != -1)
        {
            _parameters[index] = value;
        }
    }
}
