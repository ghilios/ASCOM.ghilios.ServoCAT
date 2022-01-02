#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System;
using System.Linq.Expressions;

namespace ASCOM.ghilios.ServoCAT.Utility {

    public static class Expressions {

        // https://stackoverflow.com/questions/2823236/creating-a-property-setter-delegate
        public static Func<TObject, TProperty> GetPropGetter<TObject, TProperty>(string propertyName) {
            ParameterExpression paramExpression = Expression.Parameter(typeof(TObject), "value");

            Expression propertyGetterExpression = Expression.Property(paramExpression, propertyName);

            Func<TObject, TProperty> result =
                Expression.Lambda<Func<TObject, TProperty>>(propertyGetterExpression, paramExpression).Compile();

            return result;
        }

        // https://stackoverflow.com/questions/2823236/creating-a-property-setter-delegate
        public static Action<TObject, TProperty> GetPropSetter<TObject, TProperty>(string propertyName) {
            ParameterExpression paramExpression = Expression.Parameter(typeof(TObject));

            ParameterExpression paramExpression2 = Expression.Parameter(typeof(TProperty), propertyName);

            MemberExpression propertyGetterExpression = Expression.Property(paramExpression, propertyName);

            Action<TObject, TProperty> result = Expression.Lambda<Action<TObject, TProperty>>
            (
                Expression.Assign(propertyGetterExpression, paramExpression2), paramExpression, paramExpression2
            ).Compile();

            return result;
        }
    }
}