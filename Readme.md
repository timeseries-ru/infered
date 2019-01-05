# Infered

Консольное приложение для выведения значений параметров вероятностных моделей.

## Мотивация
Существует множество отличных библиотек для вероятностного вывода, таких как
PyMC3, Stan, Pyro, Tensorflow Probablity, - однако первые два требуют для 
осуществления вывода инструментов для сборки, вторые два мне недоступны.

Поэтому были взяты библиотеки 
1. Infer.Net - вероятностный вывод,
2. Pidgin - парсер текстов,
3. CSVHelper - парсер CSV.

## Сборка и запуск
Чтобы собрать, потребуется dotnet-cli.

```
dotnet restore
dotnet publish --runtime your_runtime
```

`your_runtime` - win10-x64, ubuntu-x64 и тому подобное.

Запуск требует передачи параметров командной строки:

```
infered data.csv model_definition.model parameter_name_to_infer_1 ... parameter_name_to_infer_N
```

## CSV входных данных
Все данные, по которым будет проводиться вывод должны быть числами. Никаких boolean или strings. Real only.

## Синтаксис файла модели
Все интерпретируемые строки должны иметь формат

```
parameter_name ~ expression
```

где `expression`, это одно из

1. Определение случайной величины (ниже подробнее),
2. Математическое выражение с +, -, *, /, ^ (степень),
3. Все числа должны быть в формате #.#, то есть с плавающей точкой, даже если это целое!
4. В выражениях могут участвовать *только **определенные выше** по файлу случайные величины* или 
*величины из файла данных* (по названиям столбцов),
5. Вызов функции: Exp, Log, Logistic, Max, Min.

Определения случайной величины:
1. GaussianFromMeanAndVariance(среднее, дисперсия),
2. GammaFromShapeAndScale(размерность, масштаб),
3. BetaFromMeanAndVariance(среднее, дисперсия).

[Более подробно здесь](https://dotnet.github.io/infer/userguide/Double%20factors.html).

## Пример
В репозитории есть пример:

```
mean ~ GaussianFromMeanAndVariance(0.0, 0.1) / 2.0
alpha ~ -GaussianFromMeanAndVariance(mean, 0.1)
beta ~ (1.0 + alpha) * X1
eps ~ GaussianFromMeanAndVariance(0.0, 0.1)
Y ~ Exp(X1 + beta + eps)
```

Он при запуске

```
dotnet run example/example.csv example/example.model mean alpha eps
```

дает следующий вывод:

```
Compiling model...done.
Iterating: 
.........|.........|.........|.........|.........| 50
mean: Gaussian(0.2973, 0.02)
alpha: Gaussian(-1.486, 4.022e-146)
eps: Gaussian(0.9267, 7.046e-147)
```

И всё благодаря Infer.Net :)

## License
WTFPL.