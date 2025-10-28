using Mapbox.VectorTile.Contants;
using UnityEngine;

namespace Mapbox.VectorModule.BuildingLayerVisualizer
{
    public static class PerformanceDecodeGeometry
    {
        public static MeshVertexData GetGeometry(uint[] geometryCommands, Vector3 scale)
        {
            var vertexData = new MeshVertexData();
            vertexData.Vertices = new Vector3[geometryCommands.Length / 2]; //ArrayPool<Vector3>.Shared.Rent(geometryCommands.Length / 2);
			
            int geomCmdCnt = geometryCommands.Length;
            float cursorX = 0;
            float cursorY = 0;
            var index = 0;
            for (int i = 0; i < geomCmdCnt; i++)
            {
                uint g = geometryCommands[i];
                Commands cmd = (Commands)(g & 0x7);
                uint cmdCount = g >> 3;

                if (cmd == Commands.MoveTo || cmd == Commands.LineTo)
                {
                    for (int j = 0; j < cmdCount; j++)
                    {
                        var delta = ZigzagDecode(geometryCommands[i + 1], geometryCommands[i + 2]);
                        cursorX += (delta.x / scale.x);
                        cursorY += (delta.y / scale.z);
                        i += 2;
                        //end of part of multipart feature
                        if (cmd == Commands.MoveTo)
                        {
                            vertexData.Submeshes.Add(index);
                        }

                        var pntTmp = new Vector3(cursorX, 0, cursorY);
                        // {
                        //     x = cursorX * scaleOffset[0] + scaleOffset[2],
                        //     y = 0,
                        //     z = cursorY * scaleOffset[1] - scaleOffset[3]
                        // };
                        vertexData.Vertices[index++] = pntTmp;
                    }
                }
            }

            vertexData.Submeshes.Add(index);
            vertexData.VertexCount = index;
            return vertexData;
        }
        
        public static MeshVertexData GetGeometry(uint[] geometryCommands, Vector3 scale, Vector4 scaleOffset)
        {
            var vertexData = new MeshVertexData();
            vertexData.Vertices = new Vector3[geometryCommands.Length / 2]; //ArrayPool<Vector3>.Shared.Rent(geometryCommands.Length / 2);
			
            int geomCmdCnt = geometryCommands.Length;
            float cursorX = 0;
            float cursorY = 0;
            var index = 0;
            for (int i = 0; i < geomCmdCnt; i++)
            {
                uint g = geometryCommands[i];
                Commands cmd = (Commands)(g & 0x7);
                uint cmdCount = g >> 3;

                if (cmd == Commands.MoveTo || cmd == Commands.LineTo)
                {
                    for (int j = 0; j < cmdCount; j++)
                    {
                        var delta = ZigzagDecode(geometryCommands[i + 1], geometryCommands[i + 2]);
                        cursorX += (delta.x / scale.x);
                        cursorY += (delta.y / scale.z);
                        i += 2;
                        //end of part of multipart feature
                        if (cmd == Commands.MoveTo)
                        {
                            vertexData.Submeshes.Add(index);
                        }

                        var pntTmp = new Vector3()
                        {
                            x = cursorX * scaleOffset[0] + scaleOffset[2],
                            y = 0,
                            z = cursorY * scaleOffset[1] - scaleOffset[3]
                        };
                        vertexData.Vertices[index++] = pntTmp;
                    }
                }
            }

            vertexData.Submeshes.Add(index);
            vertexData.VertexCount = index;
            return vertexData;
        }
		
        private static Vector2 ZigzagDecode(uint x, uint y)
        {

            //TODO: verify speed improvements using
            // new Point2d(){X=x, Y=y} instead of
            // new Point3d(x, y);

            //return new Point2d(
            //    ((x >> 1) ^ (-(x & 1))),
            //    ((y >> 1) ^ (-(y & 1)))
            //);
            return new Vector2
            {
                x = ((x >> 1) ^ (-(x & 1))),
                y = ((y >> 1) ^ (-(y & 1)))
            };
        }
    }
}