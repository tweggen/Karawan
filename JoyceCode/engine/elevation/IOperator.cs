using Android.OS;
using Java.Lang.Annotation;
using Java.Lang;
using Java.Util.Functions;
using System;
using System.Collections.Generic;
using System.Text;

namespace engine.elevation
{
    public interface IOperator
    {
        /**
         * Compute the contents of the elevation fragment requested.
         *
         * @oaram elevationInterface
         *     The interface to the elevation engine. Use this interface inside the
         *     function to read the elevations from the layers below.
         * @param target
         *     The data structure to store the results of your computation into.
         *     It is granted that this target array exactly fits the size of one
         *     native fragment.
         *
         * @returns
         *     Nothing yet. Should throw an exception for unrecoverable errors.
         */
        public void ElevationOperatorProcess(
            in IElevationProvider elevationInterface,
            in Rect target
        );

        public bool ElevationOperatorIntersects(
            float x0, float z0,
            float x1, float z1
        );    
    }
}
