using Aardvark.Base;

namespace Aardvark.Physics.Sky
{
    public static class PixImageCreators
    {
        public static PixImage<byte> CreateSkyCubeMapSideC4b(
                   int cubeSide, int size, IPhysicalSky sky, C3f groundColor)
        {
            var gc = groundColor.ToC4b();
            if (cubeSide < 4)
                return PixImage.CreateCubeMapSide<byte, C4b>(
                            cubeSide, size, 4, v => v.Z < 0.0 ? gc : sky.GetRadiance(v).ToC4b());
            if (cubeSide == 4)
                return PixImage.CreateCubeMapSide<byte, C4b>(
                            cubeSide, size, 4, v => sky.GetRadiance(v).ToC4b());
            else
                return PixImage.CreateCubeMapSide<byte, C4b>(cubeSide, size, 4, v => gc);
        }

        public static PixImage<byte>[] CreateSkyCubeMapC4b(
                int size, IPhysicalSky sky, C3f groundColor)
        {
            return new PixImage<byte>[6].SetByIndex(i => CreateSkyCubeMapSideC4b(i, size, sky, groundColor));
        }

        public static PixImage<byte>[] CreateSkyCubeMapC4b(int size, IPhysicalSky sky)
        {
            return new PixImage<byte>[6].SetByIndex(i => PixImage.CreateCubeMapSide<byte, C4b>(i, size, 4,
                                                                v => sky.GetRadiance(v).ToC4b()));
        }

        public static PixImage<float>[] CreateSkyCubeMapC3f(int size, IPhysicalSky sky)
        {
            return new PixImage<float>[6].SetByIndex(i => PixImage.CreateCubeMapSide<float, C3f>(i, size, 3,
                                                                v => sky.GetRadiance(v)));
        }

        public static PixImage<float>[] CreateSkyCubeMapC4f(int size, IPhysicalSky sky)
        {
            return new PixImage<float>[6].SetByIndex(i => PixImage.CreateCubeMapSide<float, C4f>(i, size, 4,
                                                                v => sky.GetRadiance(v).ToC4f()));
        }

        public static PixImage<byte> CreateSkyCylinderC4b(int width, int height, IPhysicalSky sky)
        {
            return PixImage.CreateCylinder<byte, C4b>(width, height, 4, (phi, theta) => sky.GetRadiance(phi, theta).ToC4b());
        }

        public static PixImage<byte> CreateSkyDomeC4b(int width, int height, IPhysicalSky sky)
        {
            return PixImage.CreateDome<byte, C4b>(width, height, 4, (phi, theta) => sky.GetRadiance(phi, theta).ToC4b());
        }
    }
}
