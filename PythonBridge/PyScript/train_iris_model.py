from sklearn.datasets import load_iris
from sklearn.naive_bayes import GaussianNB
from joblib import dump

iris = load_iris()
model = GaussianNB().fit(iris.data, iris.target)
dump(model, "iris_nb.pkl")
