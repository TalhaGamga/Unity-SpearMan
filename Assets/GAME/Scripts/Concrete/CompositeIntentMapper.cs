using System.Collections.Generic;

public class CompositeIntentMapper : IIntentMapper
{
    private readonly List<IIntentMapper> _mappers = new();

    public CompositeIntentMapper(params IIntentMapper[] mappers)
    {
        _mappers.AddRange(mappers);
    }

    public ActionIntent? MapInputToIntent(InputType input, CharacterSnapshot snapshot)
    {
        foreach (var mapper in _mappers)
        {
            var intent = mapper.MapInputToIntent(input, snapshot);
            if (intent != null)
                return intent;
        }

        return null;
    }
}