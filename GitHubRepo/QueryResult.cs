namespace Community.PowerToys.Run.Plugin.GitHubRepo;

public class QueryResult<T, E>
{
	private readonly bool _success;
	private readonly T? _value;
	private readonly E? _exception;

	private QueryResult(T? v, E? e, bool success)
	{
		_success = success;
		_value = v;
		_exception = e;
	}

	public static QueryResult<T, E> Ok(T v) => new(v, default, true);
	public static QueryResult<T, E> Err(E e) => new(default, e, false);

	public static implicit operator bool(QueryResult<T, E> result) => result._success;
	public static implicit operator QueryResult<T, E>(T v) => new(v, default, true);
	public static implicit operator QueryResult<T, E>(E e) => new(default, e, false);

	public R Match<R>(Func<T, R> ok, Func<E, R> err) => _success ? ok(_value!) : err(_exception!);
}
