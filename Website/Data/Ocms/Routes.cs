using System.Collections;
using System.ComponentModel;
using System.Globalization;

namespace Website.Data.Ocms;


[TypeConverter(typeof(RouteConverter))]
public class Route : IEnumerable<(string Fix, Time? Time)>
{
	public bool Eastbound
	{
		get
		{
			var fixes = this.Select(i => OccStrips.GetOccFixPosition(i.Fix)).Where(i => i is not null).ToArray();
			if (!fixes.Any())
				return false;

			return fixes[0]!.Value.Longitude <= fixes[^1]!.Value.Longitude;
		}
	}

	public bool Westbound
	{
		get
		{
			var fixes = this.Select(i => OccStrips.GetOccFixPosition(i.Fix)).Where(i => i is not null).ToArray();
			if (!fixes.Any())
				return false;

			return fixes[0]!.Value.Longitude >= fixes[^1]!.Value.Longitude;
		}
	}

	public string[] Fixes => _fixes.ToArray();
	public Time?[] Times => Fixes.Select(f => _times.TryGetValue(f, out Time? r) ? r : null).ToArray();

	public bool Modified { get; private set; } = false;

	private readonly List<string> _fixes = new();
	private readonly Dictionary<string, Time> _times = new();

	private OccStrips? _strips;

	public Route() { }

	/// <summary>Copy constructor.</summary>
	/// <param name="other">The other item to copy.</param>
	public Route(Route other) : this()
	{
		_fixes = other._fixes;
		_times = other._times;
		Modified = other.Modified;
	}

	private readonly static char[] _routeSeparators = new[] { '\r', '\n', '\t', ' ', '.' };
	public static Route Parse(string route)
	{
		Route retval = new();

		foreach (string elem in route.Split(_routeSeparators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
			retval.Add((elem.IndexOf('/') > 4 ? elem.Split('/')[0] : elem).ToUpperInvariant());

		return retval;
	}

	public Route GetAltered(string[] newRoute)
	{
		Route retval = new(this);
		retval._fixes.Clear();
		retval._fixes.AddRange(newRoute);

		return retval;
	}

	/// <summary>Gets the previous and next three fixes along the route.</summary>
	public IEnumerable<(string Fix, Time? Time, (decimal Latitude, decimal Longitude) Coordinates)> GetDisplayedFixes(PilotTrack? lastKnownUpdate, OccStrips? strips = null)
	{
		if (lastKnownUpdate is not PilotTrack lastUpdate)
			yield break;

#pragma warning disable IDE0033
		_strips = strips ?? _strips;
		var fixes = this.Select(i => (i, OccStrips.GetOccFixPosition(i.Fix))).Where(i => i.Item2 is not null).Select(i => (i.i, i.Item2!.Value)).ToArray();

		if (fixes.Length == 0)
			// Not in scope.
			yield break;

		else if ((fixes.First().Item2.Longitude > lastUpdate.Longitude && Eastbound)
			|| (fixes.First().Item2.Longitude < lastUpdate.Longitude && Westbound))
			// Hasn't reached the first fix yet.
			foreach (var retval in fixes.Take(4))
				yield return (retval.Item1.Fix, retval.Item1.Time, retval.Item2);

		else if ((fixes.Last().Item2.Longitude < lastUpdate.Longitude && Eastbound)
			|| (fixes.Last().Item2.Longitude > lastUpdate.Longitude && Westbound))
			// Has already passed the last fix.
			foreach (var retval in fixes.TakeLast(4))
				yield return (retval.Item1.Fix, retval.Item1.Time, retval.Item2);

		else
		{
			// In our airspace.
			int passingOffset = 0;
			while (passingOffset < fixes.Length
				&& ((lastUpdate.Longitude >= fixes[passingOffset].Item2.Longitude && Eastbound)
					|| (lastUpdate.Longitude <= fixes[passingOffset].Item2.Longitude && Westbound)))
				passingOffset++;

			foreach (var retval in fixes.Skip(Math.Max(0, passingOffset - 1)).Take(4))
				yield return (retval.Item1.Fix, retval.Item1.Time, retval.Item2);
		}
#pragma warning restore IDE0033
	}

	private static IEnumerable<string> ExpandFix(string fix, OccStrips strips)
	{
		var fixArr = new[] { fix };

		if (fix.Length == 4 && fix.StartsWith("NAT", StringComparison.InvariantCultureIgnoreCase))
			return strips.GetNatTrack(char.ToUpperInvariant(fix[3])) ?? fixArr;

		return fixArr;
	}

	public void SetTime(string fix, Time time)
	{
		_times[fix] = time;
		Modified = true;
	}

	public void UnsetTime(string fix)
	{
		_times.Remove(fix);
		Modified = _times.Any();
	}

	public static bool operator ==(Route left, Route right) =>
		left._fixes.Count == right._fixes.Count && left._times.Count == right._times.Count &&
		left._fixes.All(right._fixes.Contains) &&
		left._times.All(right._times.Contains);

	public static bool operator !=(Route left, Route right) =>
		!(left == right);

	public void Add(string fix) =>
		_fixes.Add(fix);

	public void Add(string fix, Time time)
	{
		Add(fix);
		_times.Add(fix, time);
	}

	public override string ToString() => string.Join(' ', _fixes);
	public IEnumerator<(string Fix, Time? Time)> GetEnumerator() => new FixEnumerator(this);
	IEnumerator IEnumerable.GetEnumerator() => new FixEnumerator(this);
	public override bool Equals(object? obj) => obj is Route route && EqualityComparer<List<string>>.Default.Equals(_fixes, route._fixes);
	public override int GetHashCode() => HashCode.Combine(_fixes);

	private class FixEnumerator : IEnumerator<(string Fix, Time? Time)>, IEnumerator
	{
		private IEnumerable<string> Fixes => _route._strips is OccStrips s ? _route.Fixes.SelectMany(f => ExpandFix(f, s)).Distinct() : _route.Fixes;
		private readonly Route _route;
		private int _index;
		private (string Fix, Time? Time)? _current;

		internal FixEnumerator(Route route)
		{
			_route = route;
			_index = 0;
			_current = default;
		}

		public void Dispose() { }

		public bool MoveNext()
		{
			Route localRoute = _route;
			string[] fixes = Fixes.ToArray();

			if ((uint)_index < (uint)fixes.Length)
			{
				_current = (fixes[_index], localRoute._times.TryGetValue(fixes[_index], out Time? v) ? v : null);
				_index++;
				return true;
			}
			return MoveNextRare();
		}

		private bool MoveNextRare()
		{
			_index = Fixes.Count() + 1;
			_current = default;
			return false;
		}

		public (string Fix, Time? Time) Current => _current!.Value;

		object? IEnumerator.Current
		{
			get
			{
				if (_index == 0 || _index == Fixes.Count() + 1)
					throw new InvalidOperationException("EnumOpCantHappen");

				return Current;
			}
		}

		(string Fix, Time? Time) IEnumerator<(string Fix, Time? Time)>.Current => _current!.Value;

		void IEnumerator.Reset()
		{
			_index = 0;
			_current = default;
		}
	}
}

public class RouteConverter : TypeConverter
{
	private static readonly Type[] CONVERT_TYPES = new[] { typeof(string), typeof(Route) };

	public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
		CONVERT_TYPES.Contains(sourceType) || base.CanConvertFrom(context, sourceType);

	public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
		value switch {
			string strv => Route.Parse(strv),
			Route rtev => rtev.ToString(),
			_ => base.ConvertFrom(context, culture, value)
		};

	public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
		destinationType switch {
			Type t when t == typeof(string) => ((Route)value!).ToString(),
			Type t when t == typeof(Route) => Route.Parse((string)value!),
			_ => base.ConvertTo(context, culture, value, destinationType)
		};
}
