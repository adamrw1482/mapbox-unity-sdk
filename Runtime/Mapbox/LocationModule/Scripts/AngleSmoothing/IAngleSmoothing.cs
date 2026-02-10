namespace Mapbox.LocationModule.Scripts.AngleSmoothing
{
	public interface IAngleSmoothing
	{

		void Add(double angle);
		double Calculate();

	}
}
